# check for working dir
if (Test-Path -Path release) {
    # clean working dir
    Remove-Item -Recurse release\*
} else {
    New-Item -Path release -ItemType Directory | Out-Null
}

# create required directories
New-Item -Path release/tools -ItemType Directory | Out-Null

# build publish version of KnxUpdater
dotnet.exe build KnxUpdater.csproj
dotnet.exe publish KnxUpdater.csproj -c Debug -r win-x64   --self-contained true /p:PublishSingleFile=true
dotnet.exe publish KnxUpdater.csproj -c Debug -r win-x86   --self-contained true /p:PublishSingleFile=true
dotnet.exe publish KnxUpdater.csproj -c Debug -r osx-x64   --self-contained true /p:PublishSingleFile=true
dotnet.exe publish KnxUpdater.csproj -c Debug -r linux-x64 --self-contained true /p:PublishSingleFile=true

# copy package content 
Copy-Item bin/Debug/net6.0/win-x64/publish/KnxUpdater.exe   release/tools/KnxUpdater-x64.exe
Copy-Item bin/Debug/net6.0/win-x86/publish/KnxUpdater.exe   release/tools/KnxUpdater-x86.exe
Copy-Item bin/Debug/net6.0/osx-x64/publish/KnxUpdater       release/tools/KnxUpdater-osx64.exe
Copy-Item bin/Debug/net6.0/linux-x64/publish/KnxUpdater     release/tools/KnxUpdater-linux64.exe

# add necessary scripts
# Copy-Item scripts/Readme-Release.txt release/
Copy-Item scripts/Install-KnxUpdater.ps1 release/

# We execute the install script to get the current KnxUpdater-version to our bin directory for the right hardware
release/Install-KnxUpdater.ps1 release

# build release name
$version = (Get-Item release/tools/KnxUpdater-x64.exe).VersionInfo
$versionString = "$($version.FileMajorPart).$($version.FileMinorPart).$($version.FileBuildPart)"
$ReleaseName = "KnxUpdater $versionString"
$ReleaseName = $ReleaseName.Replace(" ", "-") + ".zip"

# create package 
Compress-Archive -Force -Path release/* -DestinationPath "$ReleaseName"
Remove-Item -Recurse release/*
Move-Item "$ReleaseName" release/

