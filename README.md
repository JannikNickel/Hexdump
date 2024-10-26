# Hexdump
A simple command line utility to output hex contents of files. It's written in F#/.Net8 for Windows but should work on other operating systems as well.  
*(Works similar to the linux utility but with less features)*

## Examples
```
> hexdump -c rom.bin
00000000  a9 ff 8d 02 60 a9 55 8d  00 60 a9 aa 8d 00 60 00  |....`.U..`....`.|
00000010  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  |................|
*
00007ff0  00 32 00 00 00 42 00 00  00 00 00 00 00 80 00 00  |.2...B..........|
00008000
```

```
> hexdump rom.bin
00000000 ffa9 028d a960 8d55 6000 aaa9 008d 0060
00000010 0000 0000 0000 0000 0000 0000 0000 0000
*
00007ff0 3200 0000 4200 0000 0000 0000 8000 0000
00008000
```

## Dependencies
The repo contains VS2022 solution and project files and uses .Net8.  
There are no external dependencies.

## Command line
```bash
hexdump [--help] [-canonical] [--length] [--skip] [--no-squeezing] <file>
-h, --help              # Display help dialog
-c, --canonical         # Hex + ASCII display
-n, --length <length>   # Only read <length> bytes
-s, --skip <offset>     # Skip <offset> bytes from the beginning
-v, --no-squeezing      # Dont replace identical lines with '*'
<file>                  # The file to read from
```
