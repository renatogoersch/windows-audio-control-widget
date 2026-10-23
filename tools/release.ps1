<#
.SYNOPSIS
  Cuts a release: sets <Version> in SlotMixer.csproj, commits, tags vX.Y.Z and pushes. The tag starts the "Release"
  GitHub Actions workflow, which builds both zips and publishes the GitHub Release; installed copies pick it up on
  their own within hours.

.PARAMETER Version
  The new version, X.Y.Z. CHANGELOG.md must already have a "## X.Y.Z" section.

.PARAMETER Local
  Build the zips on this machine and create the release with the GitHub CLI instead of waiting for the workflow.
#>
param(
  [Parameter(Mandatory)][string]$Version,
  [switch]$Local
)

$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Use the form X.Y.Z (for example 1.6.1), not '$Version'." }

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$tag = "v$Version"

if ((git rev-parse --abbrev-ref HEAD) -ne 'main') { throw 'Releases are cut from main.' }
if (git status --porcelain) { throw 'Commit or stash your changes first: the release commit must contain only the version.' }
if (git tag --list $tag) { throw "Tag $tag already exists." }

# Fails here, before anything is changed, when the changelog is missing.
$notes = & (Join-Path $PSScriptRoot 'release-notes.ps1') -Version $Version

$csproj = Join-Path $root 'SlotMixer\SlotMixer.csproj'
$xml = [IO.File]::ReadAllText($csproj)
$updated = [regex]::Replace($xml, '<Version>[^<]*</Version>', "<Version>$Version</Version>")
if ($updated -ne $xml) {
  [IO.File]::WriteAllText($csproj, $updated)
  git add $csproj
  git commit -m "Release $tag"
}

git tag -a $tag -m "SlotMixer $Version"
git push origin main
git push origin $tag

if ($Local) {
  & (Join-Path $PSScriptRoot 'build-zips.ps1') -ExpectVersion $Version
  $file = Join-Path $env:TEMP "slotmixer-notes-$Version.md"
  [IO.File]::WriteAllText($file, ($notes -join "`n"), (New-Object Text.UTF8Encoding($false)))
  gh release create $tag (Join-Path $root 'dist\SlotMixer-requer-dotnet.zip') (Join-Path $root 'dist\SlotMixer-autonomo.zip') `
    --title "$tag - Windows Audio Control - Widget" --notes-file $file --verify-tag
}
else {
  "Pushed $tag. The Release workflow now builds and publishes it: https://github.com/renatogoersch/windows-audio-control-widget/actions"
}
