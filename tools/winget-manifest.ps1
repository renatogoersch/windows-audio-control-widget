<#
.SYNOPSIS
  Fills the winget manifest templates in packaging\winget\ from a published GitHub Release and writes them to
  dist\winget\<version>\, ready to be submitted to microsoft/winget-pkgs (see docs/distribution.md). Runs
  "winget validate" on the result when winget is installed.

.PARAMETER Version
  X.Y.Z (or vX.Y.Z) of a Release that already exists on GitHub.

.PARAMETER Repo
  owner/name of the repository that holds the Release.

.NOTES
  The installer is the self-contained zip (SlotMixer-autonomo.zip), so the package has no .NET dependency.
  Needs the GitHub CLI (gh), logged in or not: the Release is public.
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
$templates = Join-Path $root 'packaging\winget'
$out = Join-Path $root "dist\winget\$Version"

$release = gh release view "v$Version" --repo $Repo --json assets,publishedAt | ConvertFrom-Json
$asset = $release.assets | Where-Object { $_.name -eq 'SlotMixer-autonomo.zip' } | Select-Object -First 1
if (-not $asset) { throw "Release v$Version has no SlotMixer-autonomo.zip." }

# GitHub records a SHA-256 for every asset; without one the file is downloaded and hashed here.
if ($asset.digest -and $asset.digest.StartsWith('sha256:')) {
  $sha = $asset.digest.Substring('sha256:'.Length)
}
else {
  $tmp = Join-Path ([IO.Path]::GetTempPath()) "slotmixer-$Version.zip"
  Invoke-WebRequest $asset.url -OutFile $tmp -UseBasicParsing
  $sha = (Get-FileHash $tmp -Algorithm SHA256).Hash
  Remove-Item $tmp -Force
}

$values = @{
  '__VERSION__'          = $Version
  '__RELEASE_DATE__'     = ([datetime]$release.publishedAt).ToUniversalTime().ToString('yyyy-MM-dd')
  '__INSTALLER_URL__'    = $asset.url
  '__INSTALLER_SHA256__' = $sha.ToUpper()
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force $out | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding($false)

foreach ($file in Get-ChildItem $templates -Filter *.yaml) {
  $text = [IO.File]::ReadAllText($file.FullName, [Text.Encoding]::UTF8)
  # The header comment only makes sense in the template.
  $text = ($text -split "`n" | Where-Object { $_ -notmatch '^# Template:' }) -join "`n"

  foreach ($key in $values.Keys) { $text = $text.Replace($key, $values[$key]) }
  if ($text -match '__[A-Z_]+__') { throw "Unfilled placeholder in $($file.Name): $($Matches[0])" }
  [IO.File]::WriteAllText((Join-Path $out $file.Name), $text.TrimStart("`n"), $utf8)
}

"Wrote $((Get-ChildItem $out).Count) manifests for $Version to $out"
"  InstallerUrl:    $($values['__INSTALLER_URL__'])"
"  InstallerSha256: $($values['__INSTALLER_SHA256__'])"

if (Get-Command winget -ErrorAction SilentlyContinue) {
  winget validate --manifest $out
  if ($LASTEXITCODE -ne 0) { throw 'winget validate failed.' }
}
else {
  'winget is not installed here: the manifests were written but not validated.'
}
