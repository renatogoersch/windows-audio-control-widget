<#
.SYNOPSIS
  Writes the body of a GitHub Release to the output: the "## X.Y.Z" section of CHANGELOG.md followed by the standing
  footer (which zip to download, warnings). Fails when the changelog has no section for the version, so a release
  cannot go out without saying what changed.
#>
param([Parameter(Mandatory)][string]$Version)

$ErrorActionPreference = 'Stop'
$v = $Version.TrimStart('v')
$root = Split-Path -Parent $PSScriptRoot

$changelog = Get-Content (Join-Path $root 'CHANGELOG.md') -Raw -Encoding UTF8
$section = [regex]::Match($changelog, "(?ms)^## $([regex]::Escape($v))\b[^\r\n]*\r?\n(.*?)(?=^## |\z)")
if (-not $section.Success) { throw "CHANGELOG.md has no '## $v' section." }

$footer = Get-Content (Join-Path $PSScriptRoot 'release-notes-footer.md') -Raw -Encoding UTF8
"$($section.Groups[1].Value.Trim())`n`n$($footer.Trim())`n"
