<#
.SYNOPSIS
  Rewrites bucket\slotmixer.json (the Scoop manifest; this repository is also a Scoop bucket) for a published GitHub
  Release: version, download URL and SHA-256 of SlotMixer-autonomo.zip. Commit the result to main afterwards.

.PARAMETER Version
  X.Y.Z (or vX.Y.Z) of a Release that already exists on GitHub.

.PARAMETER Repo
  owner/name of the repository that holds the Release.

.NOTES
  Scoop users get the update with "scoop update slotmixer". The same manifest has "checkver" and "autoupdate" blocks, so
  Scoop's own tooling (Excavator, "checkver -u") can also bump it; this script is the dependable, local way.
#>
param(
  [Parameter(Mandatory)][string]$Version,
  [string]$Repo = 'renatogoersch/windows-audio-control-widget'
)

$ErrorActionPreference = 'Stop'
$Version = $Version.TrimStart('v')
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Use the form X.Y.Z, not '$Version'." }
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'The GitHub CLI (gh) is needed to read the Release.' }

$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root 'bucket\slotmixer.json'

$release = gh release view "v$Version" --repo $Repo --json assets | ConvertFrom-Json
$asset = $release.assets | Where-Object { $_.name -eq 'SlotMixer-autonomo.zip' } | Select-Object -First 1
if (-not $asset) { throw "Release v$Version has no SlotMixer-autonomo.zip." }

if ($asset.digest -and $asset.digest.StartsWith('sha256:')) {
  $sha = $asset.digest.Substring('sha256:'.Length)
}
else {
  $tmp = Join-Path ([IO.Path]::GetTempPath()) "slotmixer-$Version.zip"
  Invoke-WebRequest $asset.url -OutFile $tmp -UseBasicParsing
  $sha = (Get-FileHash $tmp -Algorithm SHA256).Hash
  Remove-Item $tmp -Force
}

$repoUrl = "https://github.com/$Repo"
$json = @"
{
    "version": "$Version",
    "description": "Windows Audio Control - Widget: taskbar widget that sets the output, microphone and volume of each app",
    "homepage": "$repoUrl",
    "license": "MIT",
    "notes": "The exe is not code-signed, so Windows SmartScreen may warn on the first run. Scoop updates it with 'scoop update slotmixer'; the built-in updater only announces new versions here.",
    "architecture": {
        "64bit": {
            "url": "$($asset.url)",
            "hash": "$($sha.ToLower())"
        }
    },
    "extract_dir": "SlotMixer",
    "bin": [
        [
            "SlotMixer.exe",
            "slotmixer",
            "--portable"
        ]
    ],
    "shortcuts": [
        [
            "SlotMixer.exe",
            "SlotMixer",
            "--portable"
        ]
    ],
    "checkver": {
        "github": "$repoUrl"
    },
    "autoupdate": {
        "architecture": {
            "64bit": {
                "url": "$repoUrl/releases/download/v`$version/SlotMixer-autonomo.zip"
            }
        }
    }
}
"@

New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
[IO.File]::WriteAllText($target, ($json -replace "`r`n", "`n") + "`n", (New-Object System.Text.UTF8Encoding($false)))
$null = Get-Content $target -Raw | ConvertFrom-Json   # must still be valid JSON
"Wrote $target for $Version"
"  url:  $($asset.url)"
"  hash: $($sha.ToLower())"
