# Checks that a receiver bundle runs on its own: no MSYS2 on PATH, only the bundled DLLs and plugins.
# usage: scripts/smoke-test-receiver.ps1 <receiver-dir>
param([Parameter(Mandatory)][string]$ReceiverDir)
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path $ReceiverDir).Path
$bin = Join-Path $root 'bin'

# Same environment Receiver.cs gives uxplay.exe.
$env:PATH = "$env:SystemRoot\System32;$env:SystemRoot"
$env:GST_PLUGIN_SYSTEM_PATH_1_0 = Join-Path $root 'lib\gstreamer-1.0'
$env:GST_PLUGIN_SCANNER_1_0 = Join-Path $bin 'gst-plugin-scanner.exe'
$env:GST_REGISTRY_1_0 = Join-Path ([IO.Path]::GetTempPath()) 'better-airplay-smoke-registry.bin'
Remove-Item Env:GST_PLUGIN_PATH_1_0, Env:GST_PLUGIN_PATH -ErrorAction SilentlyContinue

$version = & (Join-Path $bin 'uxplay.exe') -v
if ($LASTEXITCODE -ne 0) { throw "uxplay.exe -v failed with exit code $LASTEXITCODE" }
Write-Host $version

# Elements UxPlay's default mirror and audio pipelines use, plus the video sink the tray app selects.
$elements = @(
    'appsrc', 'queue', 'decodebin', 'h264parse', 'h265parse', 'avdec_h264', 'avdec_h265',
    'avdec_aac', 'avdec_alac', 'videoconvert', 'videoscale', 'videoflip', 'd3d11videosink',
    'audioconvert', 'audioresample', 'volume', 'level', 'autoaudiosink', 'wasapisink', 'jpegdec'
)
$missing = $elements | Where-Object { $null = & (Join-Path $bin 'gst-inspect-1.0.exe') --exists $_; $LASTEXITCODE -ne 0 }
if ($missing) { throw "Missing GStreamer elements: $($missing -join ', ')" }
Write-Host "All $($elements.Count) GStreamer elements found."
