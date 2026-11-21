# Regenerates the placeholder art (Assets/Art) and sounds (Assets/Audio).
# Usage (from the project root):  powershell -ExecutionPolicy Bypass -File Tools/AssetGen/generate.ps1
# Then in Unity run the menu "Survivor/Setup Art & Audio" to (re)link everything.

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Resolve-Path (Join-Path $here "..\..")
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$exe = Join-Path $env:TEMP "SurvivorAssetGen.exe"

& $csc /nologo /optimize /out:$exe /r:System.Drawing.dll (Join-Path $here "ArtGen.cs") (Join-Path $here "SfxGen.cs")
if ($LASTEXITCODE -ne 0) { throw "Compile failed" }

& $exe $root
if ($LASTEXITCODE -ne 0) { throw "Generation failed" }
