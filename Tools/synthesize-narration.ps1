# Voices every tour step's narration with the Cartesia text-to-speech API.
# Usage: powershell -File Tools/synthesize-narration.ps1 [-KeyFile path] [-Voice id] [-Model sonic-3] [-Force]
# Reads Assets/Resources/Data/tour_narration.json, writes Assets/Resources/Audio/Tour/<step id>.ogg
# (mono Vorbis, via ffmpeg; the 44.1 kHz WAV from the API is kept only when ffmpeg is missing).
# Existing clips are kept unless -Force.
param(
    [string]$KeyFile = "$env:USERPROFILE\Desktop\cartesia-key.txt",
    [string]$Voice = 'b24f41fd-00a3-4cd8-992a-a0c9f13f3ef1', # Clive - Measured Expert
    [string]$Model = 'sonic-3',
    [switch]$Force
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$key = (Get-Content -Raw $KeyFile).Trim()
$narration = Get-Content -Raw (Join-Path $root 'Assets/Resources/Data/tour_narration.json') | ConvertFrom-Json
$outDir = Join-Path $root 'Assets/Resources/Audio/Tour'
New-Item -ItemType Directory -Force $outDir | Out-Null

$ffmpeg = (Get-Command ffmpeg -ErrorAction SilentlyContinue).Source
if (-not $ffmpeg) { $ffmpeg = Get-ChildItem 'C:\ffmpeg*\bin\ffmpeg.exe' -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName }
if (-not $ffmpeg) { Write-Warning 'ffmpeg not found: clips stay as WAV (about 2.5 MB each)' }

$headers = @{ 'X-API-Key' = $key; 'Cartesia-Version' = '2025-04-16' }
$total = 0.0; $made = 0
foreach ($s in $narration.steps) {
    $ogg = Join-Path $outDir ($s.id + '.ogg')
    $path = Join-Path $outDir ($s.id + '.wav')
    if ((Test-Path $ogg) -and -not $Force) { continue }
    if ((Test-Path $path) -and -not $Force) { $total += ((Get-Item $path).Length - 44) / 88200.0; continue }
    $body = @{
        model_id = $Model
        transcript = $s.narration
        voice = @{ mode = 'id'; id = $Voice }
        language = 'en'
        output_format = @{ container = 'wav'; encoding = 'pcm_s16le'; sample_rate = 44100 }
    } | ConvertTo-Json -Depth 5
    try {
        Invoke-WebRequest -Method Post -Uri 'https://api.cartesia.ai/tts/bytes' -Headers $headers `
            -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes($body)) `
            -OutFile $path -TimeoutSec 120 | Out-Null
    } catch {
        if ($Model -ne 'sonic-2') {
            Write-Host "  $Model failed for $($s.id) ($($_.Exception.Message)); retrying with sonic-2"
            $body = $body.Replace('"' + $Model + '"', '"sonic-2"')
            Invoke-WebRequest -Method Post -Uri 'https://api.cartesia.ai/tts/bytes' -Headers $headers `
                -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes($body)) `
                -OutFile $path -TimeoutSec 120 | Out-Null
        } else { throw }
    }
    $seconds = ((Get-Item $path).Length - 44) / 88200.0
    $total += $seconds; $made++
    if ($ffmpeg) {
        & $ffmpeg -v error -y -i $path -c:a libvorbis -q:a 3 -ac 1 $ogg
        if ($LASTEXITCODE -ne 0) { Write-Warning "ffmpeg failed for $($s.id); WAV kept" }
        else { try { Remove-Item $path -ErrorAction Stop } catch { Write-Warning "could not delete $($s.id).wav (open in the Editor?); delete it by hand" } }
    }
    Write-Host ("  {0,-18} {1,5:0.0} s" -f $s.id, $seconds)
}
Write-Host ("Done: {0} new clips ({1:0.0} minutes) in {2}; the Editor imports them on its next refresh" -f $made, ($total / 60), $outDir)
