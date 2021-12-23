# SplitFile

The SplitFile utility efficiently split large files into smaller ones that can be later rejoined back to recreate the original file maintaining 100% integrity independently of the original file format, encoding, or type.
SplitFile is multithreaded and optimizes IO with an internal buffer queue.
SplitFile is cross-platform and runs on Windows and Linux operating systems.

## Usage:

From a command prompt type:

```
SplitFile <filename> <splits> <destinationfolder> <IO Block size:{4KB}{8KB}{32KB}{64KB}>
```

## Where:

```<filename>``` is the file to split  
```<splits>``` is the number of splits to create.  
```<destinationfolder>``` is the destination location for the splits, please terminate the folder with / or \ accordingly.  
```<IO Block size:{4KB}{8KB}{32KB}{64KB}>``` Choose a block size that is optimal for your storage throughout. 

Example: Splitting file test.bin in 4 splits in Windows Command Prompt

```
C:\>dir /b test.bin.*
test.bin

C:\>SplitFile test.bin 4 c:\temp\ 4KB
SplitFile ver 1.0
Multi-Threaded Utility to Split Large Files

Memory Total 32674 MB, Used 9871 MB, Free 22803 MB
Splitter worker 1 created. IO block size 4 KB
Splitter worker 2 created. IO block size 4 KB
Splitter worker 3 created. IO block size 4 KB
Splitter worker 4 created. IO block size 4 KB
Worker 2 written 159 MB averaging 54 MB/s queued 76 MB
Worker 1 written 154 MB averaging 53 MB/s queued 74 MB
Worker 4 written 157 MB averaging 54 MB/s queued 82 MB
Worker 3 written 166 MB averaging 55 MB/s queued 85 MB
Duration 00:00:03.600
Done.

C:\Temp>dir /b test.bin.*
test.bin
test.bin.Split.01
test.bin.Split.02
test.bin.Split.03
test.bin.Split.04

```

To rejoin the splits, use the Windows prompt COPY command:

```
C:\Temp>copy /b test.bin.Split.000001 + /b test.bin.Split.000002 + /b test.bin.Split.000003 + /b test.bin.Split.000004 rejoined.bin
test.bin.Split.01
test.bin.Split.02
test.bin.Split.03
test.bin.Split.04
        1 file(s) copied.

```

Compare the rejoined file to original

```
C:\Temp>comp c:\test.bin rejoined.bin
Comparing test.bin and rejoined.bin...
Files compare OK

Compare more files (Y/N) ? n
```


## Requirements

SplitFile requires .net Core 3.1 (already installed in most OS). Microsoft ships official releases built and tested on Microsoft-maintained servers in Azure and supported just like any Microsoft product.

## Architecture

Splitfile creates one worker per split to read, store in the memory queue, and write to the destination file. Each worker has a reader thread, a buffer queue, and a writer thread to move data to each split. There is a maximum of 30 Splits allowed. 

## Special Considerations

a) If the writer threads fall behind the reader threads, to avoid the memory buffers to grow, the reader threads will be paused for the buffers to drain. 
You will see messages like these below, meaning that SplitFile reads faster than is writing to the splits.

```
Worker {0} Writer queue too big, pausing the reader
Worker {0} Resuming
```

b) The buffer queue's total size for all split workers is 80% of available free memory when the program starts. Each worker has its private buffer queue, and access to it is not shared.  

c) Average MB/s reported by the tool are per split. The total throughput is the sum of all split workers.  

d) If the host computer does not have the .NET core 3.1 installed the following error will occur:   

```
A fatal error occurred. The required library hostfxr.dll could not be found.
If this is a self-contained application, that library should exist in [C:\SplitFile\].
If this is a framework-dependent application, install the runtime in the global location [C:\Program Files\dotnet] or use the DOTNET_ROOT environment variable to specify the runtime location or register the runtime location in [HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\InstallLocation].

The .NET Core runtime can be found at:
  - https://aka.ms/dotnet-core-applaunch?missing_runtime=true&arch=x64&rid=win10-x64
```

To correct download and install the .NET Core runtime.

## Running on Linux

Copy SplitFile.zip to a folder in linux  
```
azureuser@myVMLinuxClient:~/SplitFile$ ls
SplitFile.zip
```

Install UnZip

```
sudo apt install unzip
```

Unzip SplitFile.zip

```
azureuser@myVMLinuxClient:~$ cd SplitFile/
unzip SplitFile.zip
Archive:  SplitFile.zip
  inflating: SplitFile.deps.json
  inflating: SplitFile.dll
  inflating: SplitFile.exe
  inflating: SplitFile.pdb
  inflating: SplitFile.runtimeconfig.json
azureuser@myVMLinuxClient:~/SplitFile$
```

Install .NET Core runtime in Ubuntu

```
wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb

sudo apt-get update; \
  sudo apt-get install -y apt-transport-https && \
  sudo apt-get update && \
  sudo apt-get install -y aspnetcore-runtime-3.1
```

Run SplitFIle with the dotnet command

```
dotnet SplitFile.dll file.bin 4 ./temp/ 4KB
SplitFile ver 1.0
Multi-Threaded Utility to Split Large Files

Splitter Worker 1 created
Splitter Worker 2 created
Splitter Worker 3 created
Splitter Worker 4 created
Duration 00:00:05.508
Done.

azureuser@myVMLinuxClient:~/SplitFile$
```