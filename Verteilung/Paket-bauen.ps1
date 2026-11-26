# Erzeugt das portable Lernendenpaket aus dem aktuellen Quellcode.
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$output = Join-Path $repo 'artifacts'
New-Item -ItemType Directory -Force $output | Out-Null
$stage = Join-Path $output ([guid]::NewGuid().ToString())
New-Item -ItemType Directory $stage | Out-Null
try {
    $program = Join-Path $stage 'Programm'
    dotnet publish (Join-Path $repo 'Tempus.Web/Tempus.Web.csproj') -c Release -r win-x64 --self-contained true -o $program
    if ($LASTEXITCODE -ne 0) { throw 'Windows-Publish fehlgeschlagen.' }
    foreach ($folder in @('Demodaten', 'Lizenzen')) {
        Copy-Item (Join-Path $repo $folder) $stage -Recurse
    }
    foreach ($name in @('Start-Tempus.cmd', 'LIESMICH.md')) {
        Copy-Item (Join-Path $PSScriptRoot $name) $stage
    }
    $source = Join-Path $stage 'Quellcode'
    New-Item -ItemType Directory $source | Out-Null
    # Die Dateiliste aus Git verhindert, dass lokale Daten oder bin/obj mitgeliefert werden.
    $paths = git -C $repo ls-files -- Tempus.sln Tempus.Core Tempus.Web .gitignore
    if ($LASTEXITCODE -ne 0) { throw 'Git-Dateiliste konnte nicht gelesen werden.' }
    foreach ($path in $paths) {
        $dest = Join-Path $source $path
        New-Item -ItemType Directory -Force (Split-Path -Parent $dest) | Out-Null
        Copy-Item -LiteralPath (Join-Path $repo $path) -Destination $dest
    }
    $zip = Join-Path $output 'Tempus_Windows_und_Quellcode.zip'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $temporaryZip = "$stage.zip"
    [System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $temporaryZip)
    Move-Item -Force $temporaryZip $zip
    Write-Output $zip
}
finally {
    Remove-Item -LiteralPath $stage -Recurse -Force
    if (Test-Path -LiteralPath "$stage.zip") { Remove-Item -LiteralPath "$stage.zip" -Force }
}
