<#
.SYNOPSIS
  Builds the two release zips into dist\: SlotMixer-requer-dotnet.zip (small, needs the .NET 8 Desktop Runtime) and
  SlotMixer-autonomo.zip (carries the runtime).

.PARAMETER ExpectVersion
  When given (for example 1.6.0 or v1.6.0), the build stops unless <Version> in the .csproj is exactly that. The release
  workflow passes the pushed tag here, so a tag and a version that disagree can never produce a release.
#>
param([string]$ExpectVersion)

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

Publish $false 'publish'
Publish $true 'publish-selfcontained'

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
  [IO.Compression.ZipFile]::CreateFromDirectory($stage, (Join-Path $dist "$zipName.zip"), [IO.Compression.CompressionLevel]::Optimal, $false)
}

New-Release 'SlotMixer-requer-dotnet' (Join-Path $proj 'publish\SlotMixer.exe') 'requer-dotnet.txt'
New-Release 'SlotMixer-autonomo' (Join-Path $proj 'publish-selfcontained\SlotMixer.exe') 'autonomo.txt'
Remove-Item (Join-Path $dist '_stage') -Recurse -Force

Get-ChildItem $dist -Filter *.zip | ForEach-Object { '{0}  {1:N1} MB' -f $_.Name, ($_.Length / 1MB) }
