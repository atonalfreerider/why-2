param(
    [Parameter(Mandatory = $true)][string]$RequestPath,
    [switch]$ValidateOnly
)

$ErrorActionPreference = 'Stop'
$request = Get-Content -LiteralPath $RequestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($null -eq $request.state -or $null -eq $request.questions) {
    throw 'Request must contain state and questions. See Tools/jev-smoke.json.'
}
$questions = @($request.questions.PSObject.Properties)
if ($questions.Count -eq 0) { throw 'At least one question is required.' }
foreach ($question in $questions) {
    $value = $question.Value
    if ($value.type -notin @('noul', 'choice', 'score') -or $null -eq $value.instructions) {
        throw "Question '$($question.Name)' needs a supported type and instructions."
    }
    if ($value.type -in @('choice', 'score') -and $null -eq $value.criteria) {
        throw "Question '$($question.Name)' needs criteria."
    }
}
# A direct TypeSafe key (TYPESAFE_API_KEY, "apikey_...") calls api.typesafe.ai; otherwise the Vercel AI Gateway.
$direct = -not [string]::IsNullOrWhiteSpace($env:TYPESAFE_API_KEY)
$body = @{
    model = $(if ($direct) { 'jev-latest' } else { 'typesafe-ai/jev' })
    state = $request.state
    questions = $request.questions
} | ConvertTo-Json -Depth 100 -Compress

if ($ValidateOnly) {
    Write-Output 'Valid request structure. No network request made.'
    exit 0
}
$gatewayKey = $(if ($direct) { $env:TYPESAFE_API_KEY } else { $env:AI_GATEWAY_API_KEY })
$keyPath = Join-Path (Split-Path $PSScriptRoot -Parent) '.jev\gateway-key.dpapi'
if ([string]::IsNullOrWhiteSpace($gatewayKey) -and (Test-Path -LiteralPath $keyPath)) {
    $secureKey = Get-Content -LiteralPath $keyPath -Raw | ConvertTo-SecureString
    $gatewayKey = [System.Net.NetworkCredential]::new('', $secureKey).Password
}
if ([string]::IsNullOrWhiteSpace($gatewayKey)) {
    throw 'Set AI_GATEWAY_API_KEY (or TYPESAFE_API_KEY) in this process before calling Jev. See Docs/jev-setup.md.'
}

$headers = @{ Authorization = "Bearer $gatewayKey" }
try {
    $result = Invoke-RestMethod -Method Post `
        -Uri $(if ($direct) { 'https://api.typesafe.ai/v1/systemone' } else { 'https://ai-gateway.vercel.sh/typesafe/v1/systemone' }) `
        -Headers $headers -ContentType 'application/json' `
        -Body ([System.Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 60
} catch {
    # Do not print headers, credentials, request state, or raw server errors.
    $status = $_.Exception.Response.StatusCode
    throw "Jev request failed (HTTP $status). Check the key, Gateway balance, and provider availability."
}
if ($null -eq $result.answers) { throw 'Jev returned no answers.' }
$result | ConvertTo-Json -Depth 100
