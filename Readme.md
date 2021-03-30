# About
A simple command line interface to change .cbr and .cbz comic files with png or jpeg images to webp, improving compression markedly

# Setup
This project uses [libwebp-net](https://github.com/imazen/libwebp-net) and [libwebp.dll](https://s3.amazonaws.com/resizer-dynamic-downloads/webp/0.5.2/x86_64/libwebp.dll).

# Running
Run `dotnet run -- [args]`  
`dotnet run -- --help` for help
```
  -i|--input <FOLDER/FIlE>  The file or folder to convert
  -o|--output <FOLDER>      Base path of the output (will be in output/subfolders if the recursive option is enabled), default: converted_comics
  -r|--recursive            Recursively traverse the input folder (include all subfolders)
  -s|--skip                 Skip processing file if it already exists in the output folder
  -q|--quality              Quality to use for the webp files (default: 75)
  -p|--parallel             Run in parallel, utilizing all computing resources
  -m|--multi-processing     Number of images to be processed at a time (better not changed when using Parallel mode), (default: 1)
  -?|-h|--help              Show help information
  ```

# Building
Run `dotnet publish -r win-x64 -c Release`

