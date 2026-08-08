$ErrorActionPreference = 'Stop'

$binDir = Join-Path $env:LOCALAPPDATA 'Kivoy\bin'
$tmpDir = Join-Path $env:TEMP 'tubesetup'
New-Item -ItemType Directory -Force -Path $binDir | Out-Null
New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null

Write-Host ''
Write-Host '==============================' -ForegroundColor Cyan
Write-Host ' Kivoy - Engine Setup' -ForegroundColor Cyan
Write-Host '==============================' -ForegroundColor Cyan
Write-Host "Engines folder: $binDir"
Write-Host ''

function Download([string]$Name, [string]$Url, [string]$Out) {
    Write-Host "Downloading $Name ..."
    & curl.exe -L --fail --retry 3 --progress-bar -o $Out $Url
    if ($LASTEXITCODE -ne 0) { throw "Failed to download $Name" }
    Write-Host "   $Name downloaded."
    Write-Host ''
}

# yt-dlp - the core downloader (single exe)
Download 'yt-dlp' 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe' (Join-Path $binDir 'yt-dlp.exe')

# deno - JS runtime (zip)
$denoZip = Join-Path $tmpDir 'deno.zip'
Download 'deno (JS runtime)' 'https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip' $denoZip
Write-Host 'Extracting deno ...'
& tar.exe -xf $denoZip -C $binDir deno.exe
if ($LASTEXITCODE -ne 0) { throw 'Failed to extract deno' }
Remove-Item $denoZip -Force -ErrorAction SilentlyContinue
Write-Host '   deno extracted.'
Write-Host ''

# ffmpeg + ffprobe - media processing (zip)
$ffZip = Join-Path $tmpDir 'ffmpeg.zip'
Download 'ffmpeg + ffprobe' 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip' $ffZip
Write-Host 'Extracting ffmpeg ...'
$ffDir = Join-Path $tmpDir 'ffmpeg'
New-Item -ItemType Directory -Force -Path $ffDir | Out-Null
& tar.exe -xf $ffZip -C $ffDir
if ($LASTEXITCODE -ne 0) { throw 'Failed to extract ffmpeg' }
$ffmpeg = Get-ChildItem $ffDir -Recurse -Filter ffmpeg.exe -ErrorAction SilentlyContinue | Select-Object -First 1
$ffprobe = Get-ChildItem $ffDir -Recurse -Filter ffprobe.exe -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $ffmpeg -or -not $ffprobe) { throw 'ffmpeg.exe / ffprobe.exe not found in archive' }
Copy-Item $ffmpeg.FullName (Join-Path $binDir 'ffmpeg.exe') -Force
Copy-Item $ffprobe.FullName (Join-Path $binDir 'ffprobe.exe') -Force
Remove-Item $ffZip, $ffDir -Force -Recurse -ErrorAction SilentlyContinue
Write-Host '   ffmpeg extracted.'
Write-Host ''

Write-Host '==============================' -ForegroundColor Green
Write-Host ' Engine setup complete.' -ForegroundColor Green
Write-Host '==============================' -ForegroundColor Green
Write-Host ''
