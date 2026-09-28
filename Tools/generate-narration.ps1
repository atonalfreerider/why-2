# Generates the spoken narration for every step of the guided tour with the OpenAI API.
# Usage: powershell -File Tools/generate-narration.ps1 [-KeyFile path] [-Model gpt-5]
# Reads Assets/Resources/Data/tour.json, writes Assets/Resources/Data/tour_narration.json.
param(
    [string]$KeyFile = "$env:USERPROFILE\Desktop\oai-key.txt",
    [string]$Model = "gpt-5"
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$key = (Get-Content -Raw $KeyFile).Trim()
$tour = Get-Content -Raw (Join-Path $root 'Assets/Resources/Data/tour.json') | ConvertFrom-Json

$system = @'
You write the spoken narration for "Why", an interactive causality graph that runs from the Big Bang to the present moment. A viewer watches a guided tour of 63 stops; at each stop the camera flies to a point on the graph and your narration is read aloud by a text-to-speech voice.

The graph: time runs clockwise around a super-logarithmic clock of deep time (Big Bang at 6 o'clock, life near 10 o'clock); at 3 o'clock, 5,000 years ago, human history leaves the circle on a straight line to now. Color means one thing only: red is matter, green is life, blue is humans, stacked bottom to top because each level grew out of the one beneath it. Distance from the inner track is relevance to us: the inner track is our own lineage (universe, Laniakea, Milky Way, Sun, Earth, first cells, animals, apes, civilizations, you); everything that split off drifts outward and fades. A faint grid fans out of the Big Bang showing the orders of magnitude by which space expanded. Beneath the civilizations lie the livestock and crops people raise and the minerals they mine. Inside each civilization, individual lifelines rise and fall with social market value; famous figures glow.

Themes to weave through the whole tour, returning to them naturally as the stops progress:
1. The three levels of the universe (matter, life, humans) and how each level is built on everything that came before it.
2. Cause and effect as a one-way arrow: nothing here happens without what preceded it, and nothing can run backward.
3. How scale works: billions of years compressed into a clock, orders of magnitude of expansion, and why the graph zooms and re-scales.
4. The chain of significant causes that eventually led to you and me watching this tutorial right now.

Style: warm, clear, spoken English for a general audience; second person ("you"); one steady narrator voice; every stop 60 to 110 words; plain text only (no markdown, no bullet points, no stage directions); write numbers the way they should be read aloud (say "thirteen point eight billion years", "ten to the twenty-sixth"); stay consistent with the on-screen text and its facts; each stop should stand on its own but flow from the previous one. The final stop must land on the present moment: you, watching this.

Return only a JSON object: {"steps":[{"id":"<step id>","narration":"<spoken text>"}, ...]} with exactly one entry per step, in order.
'@

$stepsForPrompt = @()
foreach ($s in $tour.steps) {
    $stepsForPrompt += [pscustomobject]@{ id = $s.id; title = $s.title; onScreenText = $s.text; view = $s.focus; pointsAt = $s.anchor }
}
$userMsg = "Tour title: $($tour.title)`n`nSteps (in order):`n" + ($stepsForPrompt | ConvertTo-Json -Depth 5)

$body = @{
    model = $Model
    response_format = @{ type = 'json_object' }
    messages = @(
        @{ role = 'system'; content = $system },
        @{ role = 'user'; content = $userMsg }
    )
} | ConvertTo-Json -Depth 8

Write-Host "Requesting narration for $($tour.steps.Count) steps from $Model ..."
$resp = Invoke-RestMethod -Method Post -Uri 'https://api.openai.com/v1/chat/completions' `
    -Headers @{ Authorization = "Bearer $key" } -ContentType 'application/json; charset=utf-8' `
    -Body ([System.Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 900
$content = $resp.choices[0].message.content
$parsed = $content | ConvertFrom-Json

# validate: one narration per step, in order
$byId = @{}
foreach ($n in $parsed.steps) { $byId[$n.id] = $n.narration }
$missing = @($tour.steps | Where-Object { -not $byId.ContainsKey($_.id) -or [string]::IsNullOrWhiteSpace($byId[$_.id]) } | ForEach-Object { $_.id })
if ($missing.Count -gt 0) { throw "Missing narration for: $($missing -join ', ')" }

$out = [ordered]@{
    model = $Model
    generated = (Get-Date).ToString('yyyy-MM-dd')
    steps = @($tour.steps | ForEach-Object { [ordered]@{ id = $_.id; narration = $byId[$_.id].Trim() } })
}
$json = ($out | ConvertTo-Json -Depth 5).Replace("`r`n", "`n")
# ConvertTo-Json escapes every non-ASCII character (curly quotes, dashes) as \uXXXX: write them as UTF-8
$evaluator = [System.Text.RegularExpressions.MatchEvaluator]{ param($m) [string][char][Convert]::ToInt32($m.Groups[1].Value, 16) }
$json = [regex]::Replace($json, '\\u([0-9a-fA-F]{4})', $evaluator)
$path = Join-Path $root 'Assets/Resources/Data/tour_narration.json'
[IO.File]::WriteAllText($path, $json + "`n", (New-Object System.Text.UTF8Encoding($false)))
$words = ($out.steps | ForEach-Object { ($_.narration -split '\s+').Count } | Measure-Object -Sum -Minimum -Maximum)
Write-Host "Wrote $path : $($out.steps.Count) steps, $($words.Sum) words (min $($words.Minimum), max $($words.Maximum) per step); usage: $($resp.usage.prompt_tokens) in / $($resp.usage.completion_tokens) out"
