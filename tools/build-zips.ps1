<#
.SYNOPSIS
  Builds the two release zips into dist\: SlotMixer-requer-dotnet.zip (small, needs the .NET 8 Desktop Runtime) and
  SlotMixer-autonomo.zip (carries the runtime).

  Two phases: (1) build the two exes, (2) put each in a zip with its readme. Without the optional parameters below the
  script does exactly that, in that order; they only exist so the release workflow can sign the exes in between.

.PARAMETER ExpectVersion
  When given (for example 1.6.0 or v1.6.0), the build stops unless <Version> in the .csproj is exactly that. The release
  workflow passes the pushed tag here, so a tag and a version that disagree can never produce a release.

.PARAMETER ExeOutDir
  Also copies the two freshly built exes to this folder as SlotMixer-requer-dotnet.exe and SlotMixer-autonomo.exe
  (the names the signing request expects).

.PARAMETER NoZips
  Stops after phase 1 (use with -ExeOutDir); dist\ is left alone.

.PARAMETER SignedExeDir
  Skips phase 1 and takes SlotMixer-requer-dotnet.exe and SlotMixer-autonomo.exe from this folder (the same two exes,
  already code-signed) for the zips. Each must be the version being built.
#>
param(
  [string]$ExpectVersion,
  [string]$ExeOutDir,
  [switch]$NoZips,
  [string]$SignedExeDir
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'SlotMixer'
$dist = Join-Path $root 'dist'
$text = Join-Path $PSScriptRoot 'leia-me'

$version = ([xml](Get-Content (Join-Path $proj 'SlotMixer.csproj') -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if ($ExpectVersion -and $ExpectVersion.TrimStart('v') -ne $version) {
  throw "The tag says $ExpectVersion but SlotMixer.csproj says $version. Fix <Version> and tag again."
}
"Building SlotMixer $version"

function Publish($selfContained, $folder) {
  $publishArgs = @('publish', (Join-Path $proj 'SlotMixer.csproj'), '-c', 'Release', '-r', 'win-x64',
            '--self-contained', $selfContained.ToString().ToLower(), '-p:PublishSingleFile=true', '-p:DebugType=none',
            '-o', (Join-Path $proj $folder), '-nologo', '-v', 'q')
  if ($selfContained) { $publishArgs += '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:EnableCompressionInSingleFile=true' }
  & dotnet @publishArgs
  if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($folder)" }

  # The updater picks its zip from this flag; a wrong value would send people the wrong kind of update.
  $info = Get-Item (Join-Path $proj 'obj\Release\net8.0-windows\win-x64\SlotMixer.AssemblyInfo.cs')
  $flag = if ($selfContained) { 'AssemblyMetadata("SelfContained", "true")' } else { 'AssemblyMetadata("SelfContained", "false")' }
  if (-not (Select-String -Path $info.FullName -SimpleMatch -Pattern $flag -Quiet)) {
    throw "Expected $flag in $($info.FullName) after publishing $folder"
  }
}

# ---- phase 1: the two exes ---------------------------------------------------------------------------------------
if ($SignedExeDir) {
  $exeLite = Join-Path $SignedExeDir 'SlotMixer-requer-dotnet.exe'
  $exeFull = Join-Path $SignedExeDir 'SlotMixer-autonomo.exe'
  foreach ($exe in $exeLite, $exeFull) {
    if (-not (Test-Path $exe)) { throw "Signed exe not found: $exe" }
    # The signing step must hand back the build it was given, not some other one.
    $fileVersion = (Get-Item $exe).VersionInfo.FileVersion
    if (-not "$fileVersion".StartsWith("$version.")) { throw "$exe is version $fileVersion, expected $version." }
  }
  "Using the signed exes from $SignedExeDir"
}
else {
  Publish $false 'publish'
  Publish $true 'publish-selfcontained'
  $exeLite = Join-Path $proj 'publish\SlotMixer.exe'
  $exeFull = Join-Path $proj 'publish-selfcontained\SlotMixer.exe'
}

if ($ExeOutDir) {
  New-Item -ItemType Directory -Force $ExeOutDir | Out-Null
  Copy-Item $exeLite (Join-Path $ExeOutDir 'SlotMixer-requer-dotnet.exe') -Force
  Copy-Item $exeFull (Join-Path $ExeOutDir 'SlotMixer-autonomo.exe') -Force
}
if ($NoZips) { return }

# ---- phase 2: the zips -------------------------------------------------------------------------------------------
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Force $dist | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem

function New-Release($zipName, $exe, $noteFile) {
  $stage = Join-Path $dist "_stage\$zipName"
  $dir = Join-Path $stage 'SlotMixer'
  New-Item -ItemType Directory -Force $dir | Out-Null
  Copy-Item $exe (Join-Path $dir 'SlotMixer.exe')
  $utf8 = New-Object System.Text.UTF8Encoding($true)
  $leiame = [IO.File]::ReadAllText((Join-Path $text 'comum.txt'), [Text.Encoding]::UTF8) +
            [IO.File]::ReadAllText((Join-Path $text $noteFile), [Text.Encoding]::UTF8)
  [IO.File]::WriteAllText((Join-Path $dir 'LEIA-ME.txt'), $leiame, $utf8)

  # The English readme is optional: comum.en.txt plus requer-dotnet.en.txt / autonomo.en.txt. Without them, no README.txt.
  $common = Join-Path $text 'comum.en.txt'
  $note = Join-Path $text ([IO.Path]::ChangeExtension($noteFile, 'en.txt'))
  if ((Test-Path $common) -and (Test-Path $note)) {
    $readme = [IO.File]::ReadAllText($common, [Text.Encoding]::UTF8) + [IO.File]::ReadAllText($note, [Text.Encoding]::UTF8)
    [IO.File]::WriteAllText((Join-Path $dir 'README.txt'), $readme, $utf8)
  }

  [IO.Compression.ZipFile]::CreateFromDirectory($stage, (Join-Path $dist "$zipName.zip"), [IO.Compression.CompressionLevel]::Optimal, $false)
}

New-Release 'SlotMixer-requer-dotnet' $exeLite 'requer-dotnet.txt'
New-Release 'SlotMixer-autonomo' $exeFull 'autonomo.txt'
Remove-Item (Join-Path $dist '_stage') -Recurse -Force

Get-ChildItem $dist -Filter *.zip | ForEach-Object { '{0}  {1:N1} MB' -f $_.Name, ($_.Length / 1MB) }
