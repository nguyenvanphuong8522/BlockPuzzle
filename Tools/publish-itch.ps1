# Pushes the WebGL build (Build/WebGL) to itch.io with butler. Only changed files are uploaded.
# Usage (from the project root):
#   powershell -ExecutionPolicy Bypass -File Tools/publish-itch.ps1 -Target yourname/your-game
# The target is saved in Tools/itch-target.txt, so next time just run the script without -Target.
# First time only: run  Tools\butler\butler.exe login  and approve in the browser.
# (butler itself lives in Tools\butler, downloaded from https://broth.itch.zone/butler, not committed to git.)

param([string]$Target)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$butler = Join-Path $PSScriptRoot "butler\butler.exe"
$targetFile = Join-Path $PSScriptRoot "itch-target.txt"
$build = Join-Path $root "Build\WebGL"

if (-not (Test-Path $butler)) { throw "butler not found at $butler" }
if (-not (Test-Path (Join-Path $build "index.html"))) { throw "No WebGL build in $build - run Build > Build WebGL in Unity first." }

if ($Target) { Set-Content -Path $targetFile -Value $Target -Encoding ascii }
elseif (Test-Path $targetFile) { $Target = (Get-Content $targetFile -Raw).Trim() }
else { throw "Pass -Target yourname/your-game (from your itch.io page URL: https://yourname.itch.io/your-game)" }

$version = Get-Date -Format "yyyy.MM.dd.HHmm"
Write-Host "Pushing $build to ${Target}:html5 (version $version)"
& $butler push $build "${Target}:html5" --userversion $version
if ($LASTEXITCODE -ne 0) { throw "butler push failed" }
& $butler status "${Target}:html5"
