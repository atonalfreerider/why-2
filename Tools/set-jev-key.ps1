$ErrorActionPreference = 'Stop'
$secureKey = Read-Host 'Vercel AI Gateway API key' -AsSecureString
if ($secureKey.Length -eq 0) { throw 'A key is required.' }
$keyDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) '.jev'
New-Item -ItemType Directory -Path $keyDirectory -Force | Out-Null
$secureKey | ConvertFrom-SecureString | Set-Content `
    -LiteralPath (Join-Path $keyDirectory 'gateway-key.dpapi') -Encoding ASCII
Write-Output 'Saved a Windows-user-encrypted key locally. The .jev directory is excluded from Git.'
