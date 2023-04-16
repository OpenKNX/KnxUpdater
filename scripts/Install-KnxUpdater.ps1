Import-Module BitsTransfer

# check for working dir
if (!(Test-Path -Path ~/bin)) {
    New-Item -Path ~/bin -ItemType Directory | Out-Null
}

Write-Host "Kopiere KnxUpdater..."

$os = "??-Bit"
$setExecutable = 0

Copy-Item tools/bossac* ~/bin/
if ($?) {
    if ($Env:OS -eq "Windows_NT") {
        if ([Environment]::Is64BitOperatingSystem)
        {
            $os="Windows 64-Bit"
            Copy-Item tools/KnxUpdater-x64.exe ~/bin/KnxUpdater.exe
        }
        else
        {
            $os="Windows 32-Bit"
            Copy-Item tools/KnxUpdater-x86.exe ~/bin/KnxUpdater.exe
        }
    } elseif ($IsMacOS) {
        $os = "Mac OS"
        $setExecutable = 1
        Copy-Item tools/KnxUpdater-osx64.exe ~/bin/KnxUpdater
    } elseif ($IsMacOS) {
        $os = "Linux OS"
        $setExecutable = 1
        Copy-Item tools/KnxUpdater-linux64.exe ~/bin/KnxUpdater
    }
}
if (!$?) {
    Write-Host "Kopieren fehlgeschlagen, KnxUpdater ist nicht verfuegbar. Bitte versuchen Sie es erneut."
    timeout /T 20
    Exit 1
}
$version = (Get-Item ~/bin/KnxUpdater.exe).VersionInfo
$versionString = "$($version.FileMajorPart).$($version.FileMinorPart).$($version.FileBuildPart)"

Write-Host "
    Folgende Tools ($os) wurden im Verzeichnis ~/bin verfuegbar gemacht:
        KnxUpdater $versionString - OpenKNX Firmware Update ueber den KNX-Bus
"
if ($setExecutable) {
    Write-Host "ACHTUNG: Die Datei ~/bin/KnxUpdater muss not mit chmod +x ausfuehrbar gemacht werden. Dies muss ueber Kommandozeile geschehen, solange wir keine andere Loesung hierfuer gefunen haben."
}

timeout /T 20
