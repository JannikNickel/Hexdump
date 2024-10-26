open System
open System.IO

module Messages = 
    [<Literal>]
    let errStr = "Use 'hexdump --help' for more information about supported options!"
    [<Literal>]
    let errExpectedValue = "Expected value after %s!"
    [<Literal>]
    let errNegativeInt = "Value for parameter %s can not be negative!"
    [<Literal>]
    let errInvalidInt = "Invalid integer value for %s!"
    [<Literal>]
    let errUnknownParameter = "Invalid option: %s!"
    [<Literal>]
    let errFileDuplicate = "<file> has already been specified!"
    [<Literal>]
    let errMissingFileInput = "<file> has not been specified!"
    [<Literal>]
    let errFileNotExist = "File '%s' does not exist!"
    [<Literal>]
    let helpStr =
        "hexdump [--help] [-canonical] [--length] [--skip] [--no-squeezing] <file>\n\n\
        -h, --help\n\tDisplay this help dialog\n\
        -c, --canonical\n\tHex + ASCII display\n\
        -n, --length <length>\n\tOnly read <length> bytes\n\
        -s, --skip <offset>\n\tSkip <offset> bytes from the beginning\n\
        -v, --no-squeezing\n\tDont replace identical lines with a '*'\n\
        <file>\n\tThe file to read from"

[<Literal>]
let windowSize = 16

type CliOptions = {
    help: bool
    canonical: bool
    length: int32 option
    offset: int32 option
    noSqueezing: bool
    file: string option }
with
    static member Default = {
        help = false
        canonical = false
        length = None
        offset = None
        noSqueezing = false
        file = None }

type DumpState = {
    addr: uint64
    prev: byte array
    prevEqual: bool
    isSkipped: bool }
with
    static member Default = {
        addr = 0UL
        prev = [||]
        prevEqual = false
        isSkipped = false }

let parseCli argv = 
    let matchNum param (tail: string list) = 
        match tail with
        | [] -> Error (sprintf Messages.errExpectedValue param)
        | head::_ ->
            match (Int32.TryParse head) with
            | (true, value) when value < 0 -> Error (sprintf Messages.errNegativeInt param)
            | (true, value) -> Ok value
            | (false, _ ) -> Error (sprintf Messages.errInvalidInt param)

    let rec parseArg args options = 
        match args with
        | [] -> options
        | ("--help" | "-h")::tail -> parseArg tail { options with help = true }
        | ("--canonical" | "-c")::tail -> parseArg tail { options with canonical = true }
        | ("--no-squeezing" | "-v")::tail -> parseArg tail { options with noSqueezing = true }
        | (("--length" | "-n") as lengthParam)::tail ->
            match matchNum lengthParam tail with
            | Ok value -> parseArg tail[1..] { options with length = Some value }
            | Error err -> failwith err
        | (("--skip" | "-s") as offsetParam)::tail ->
            match matchNum offsetParam tail with
            | Ok value -> parseArg tail[1..] { options with offset = Some value }
            | Error err -> failwith err
        | head::_ when head.StartsWith '-' -> failwith (sprintf Messages.errUnknownParameter head)
        | head::tail ->
            match options.file with
            | Some _ -> failwith Messages.errFileDuplicate
            | None -> parseArg tail { options with file = Some head }

    parseArg argv CliOptions.Default

let readBytes (file: string) = 
    seq {
        use fs = new FileStream (file, FileMode.Open, FileAccess.Read)
        let buffer = Array.zeroCreate<byte> 1024
        let mutable read = fs.Read (buffer, 0, buffer.Length)
        while read > 0 do
            for i in 0..(read - 1) do
                yield buffer[i]
            read <- fs.Read (buffer, 0, buffer.Length)
    }

let printHex (bytes: byte array) (addr: uint64) (canonical: bool) = 
    let endianByteOrder (pair: byte * byte) = 
        match BitConverter.IsLittleEndian with
        | true -> (snd pair, fst pair)
        | false -> pair

    let fmtHexPair (pair: byte array) = 
        match pair with
        | [|b0; b1|] -> Some (b0, b1)
        | [|b0|] -> Some (b0, 0uy)
        | _ -> None        
        |> function
            | Some (b0, b1) ->
                let (b0', b1') = endianByteOrder (b0, b1)
                sprintf "%02x%02x" b0' b1'
            | None -> ""

    let fmtHexByte (b: byte) = sprintf "%02x" b

    let hexStr = 
        if canonical then
            bytes
            |> Array.mapi (fun i b -> 
                let hex = fmtHexByte b
                if i = (windowSize / 2 - 1) then hex + " " else hex
            )
            |> String.concat " "
        else
            bytes
            |> Array.chunkBySize 2
            |> Array.map fmtHexPair
            |> String.concat " "

    let ascii () = 
        bytes
        |> Array.map (fun b -> if b >= 32uy && b <= 126uy then char b else '.')
        |> String
        |> fun s -> if s <> "" then sprintf "|%s|" s else s

    let paddingLength = max 0 (60 - 8 - 2 - hexStr.Length - 2)
    let padding = String.replicate (paddingLength) " "
    let line =
        match canonical with
        | true -> sprintf "%08x  %s  %s%s" addr hexStr padding (ascii ())
        | false -> sprintf "%08x %s" addr hexStr

    printfn "%s" line

let hexdump (options: CliOptions) = 
    let fSize = FileInfo(options.file.Value).Length
    let bytes = readBytes options.file.Value
    let offset = options.offset |> Option.defaultValue 0 |> min (int fSize)
    let length = options.length |> Option.defaultValue Int32.MaxValue

    let res = 
        bytes
        |> Seq.skip offset
        |> Seq.truncate length
        |> Seq.chunkBySize windowSize
        |> Seq.fold (fun state window -> 
            let arrEqual arrA arrB = 
                Array.length arrA = Array.length arrB && Array.forall2 (fun a b -> a = b) arrA arrB

            let prevEqual = arrEqual state.prev window
            let skip = prevEqual && not options.noSqueezing        
            let next = {
                addr = state.addr + uint64 (Array.length window)
                prevEqual = prevEqual
                prev = window
                isSkipped = skip }

            match skip, state.isSkipped with
            | true, false -> printfn "*"
            | false, _ -> printHex window state.addr options.canonical
            | _ -> ()

            next
        ) { DumpState.Default with addr = uint64 offset }

    printHex [||] res.addr options.canonical

[<EntryPoint>]
let main argv = 
    try
        let options = parseCli (List.ofArray argv)
        match options with
        | { help = true } -> printfn Messages.helpStr
        | { file = None } -> printfn Messages.errMissingFileInput
        | { file = Some f } when not <| File.Exists f -> printfn Messages.errFileNotExist f
        | _ -> hexdump options
        0
    with
    | ex -> 
        eprintfn "%s" ex.Message
        1
