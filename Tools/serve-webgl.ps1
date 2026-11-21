# Tiny local web server for testing the WebGL build (browsers refuse to run it from file://).
# Usage (from the project root):  powershell -ExecutionPolicy Bypass -File Tools/serve-webgl.ps1
# Then open http://localhost:8080  (Ctrl+C to stop).

param([int]$Port = 8080)

$root = Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")) "Build\WebGL"
if (-not (Test-Path (Join-Path $root "index.html"))) { throw "No WebGL build in $root - run Build > Build WebGL in Unity first." }

$types = @{
    ".html" = "text/html; charset=utf-8"; ".js" = "application/javascript"; ".css" = "text/css"
    ".wasm" = "application/wasm"; ".unityweb" = "application/octet-stream"; ".data" = "application/octet-stream"
    ".png" = "image/png"; ".ico" = "image/x-icon"; ".json" = "application/json"
}

$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:$Port/")
$listener.Start()
Write-Host "Serving $root at http://localhost:$Port/  (Ctrl+C to stop)"

try {
    while ($listener.IsListening) {
        $context = $listener.GetContext()
        $response = $context.Response
        try {
            $path = [Uri]::UnescapeDataString($context.Request.Url.AbsolutePath.TrimStart('/'))
            if ($path -eq "") { $path = "index.html" }
            $file = [IO.Path]::GetFullPath((Join-Path $root $path))
            if ($file.StartsWith($root) -and (Test-Path $file -PathType Leaf)) {
                $ext = [IO.Path]::GetExtension($file).ToLower()
                $response.ContentType = if ($types.ContainsKey($ext)) { $types[$ext] } else { "application/octet-stream" }
                $bytes = [IO.File]::ReadAllBytes($file)
                $response.ContentLength64 = $bytes.Length
                if ($context.Request.HttpMethod -ne "HEAD") { $response.OutputStream.Write($bytes, 0, $bytes.Length) }
            } else {
                $response.StatusCode = 404
            }
        } catch {
            Write-Host "Request failed: $_"
        } finally {
            $response.Close()
        }
    }
} finally {
    $listener.Stop()
}
