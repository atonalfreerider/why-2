# Compiles C# scripts under Assets/ against the Unity reference assemblies, outside the Editor.
# Usage: powershell -File Tools/compile-check.ps1 [-Folders Scripts/Core,Scripts/Life] [-Editor]
# With no -Folders, compiles every runtime script under Assets (excluding Editor folders and TextMesh Pro).
# -Editor compiles Assets/**/Editor scripts together with Assets/Scripts against the editor references.
param(
    [string[]]$Folders = @(),
    [switch]$Editor
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'Assembly-CSharp.csproj'
if ($Editor -and (Test-Path (Join-Path $root 'Assembly-CSharp-Editor.csproj'))) {
    $src = Join-Path $root 'Assembly-CSharp-Editor.csproj'
}
if (-not (Test-Path $src)) { Write-Error "Missing $src - open the project in Unity once so it generates project files." }

# unique name so parallel checks do not collide
$name = 'CompileCheck_' + [Guid]::NewGuid().ToString('N').Substring(0, 8)

$xml = Get-Content -Raw $src
# drop the explicit file items Unity generated; we glob instead
$xml = [regex]::Replace($xml, '\s*<Compile Include="[^"]*"\s*/>', '')
$xml = [regex]::Replace($xml, '\s*<None Include="[^"]*"\s*/>', '')
$xml = [regex]::Replace($xml, '\s*<ProjectReference Include="[^"]*"\s*/>', '')
$xml = [regex]::Replace($xml, '<AssemblyName>[^<]*</AssemblyName>', "<AssemblyName>$name</AssemblyName>")
$xml = $xml.Replace('Temp\obj\$(MSBuildProjectName)', "Temp\compilecheck\$name\obj")
$xml = [regex]::Replace($xml, '<OutputPath>[^<]*</OutputPath>', "<OutputPath>Temp\compilecheck\$name\bin\</OutputPath>")

$exclude = 'Assets\**\Editor\**\*.cs;Assets\TextMesh Pro\**\*.cs'
if ($Editor) {
    $items = '<Compile Include="Assets\**\Editor\**\*.cs" Exclude="Assets\TextMesh Pro\**\*.cs" />'
    $items += '<Compile Include="Assets\Scripts\**\*.cs" Exclude="' + $exclude + '" />'
} elseif ($Folders.Count -gt 0) {
    $items = ($Folders | ForEach-Object { '<Compile Include="Assets\' + ($_ -replace '/', '\') + '\**\*.cs" Exclude="' + $exclude + '" />' }) -join ''
} else {
    $items = '<Compile Include="Assets\**\*.cs" Exclude="' + $exclude + '" />'
}
$xml = [regex]::Replace($xml, '</Project>\s*$', "<ItemGroup>$items</ItemGroup></Project>")

$out = Join-Path $root "$name.csproj"
Set-Content -Path $out -Value $xml -Encoding UTF8
Push-Location $root
try {
    $log = & dotnet build $out -nologo -v q -clp:NoSummary 2>&1
    $errors = $log | Where-Object { $_ -match ': error ' } | ForEach-Object { $_ -replace '\s*\[[^\]]*\.csproj\]$', '' } | Sort-Object -Unique
    $warnings = $log | Where-Object { $_ -match ': warning CS' -and $_ -notmatch 'CS0169|CS0414|CS0649|CS8618' } | ForEach-Object { $_ -replace '\s*\[[^\]]*\.csproj\]$', '' } | Sort-Object -Unique
    $warnings | Select-Object -First 30 | ForEach-Object { Write-Output $_ }
    if ($errors) { $errors | ForEach-Object { Write-Output $_ }; Write-Output "COMPILE FAILED ($(@($errors).Count) errors)"; exit 1 }
    Write-Output 'COMPILE OK'
} finally {
    Remove-Item $out -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $root "Temp\compilecheck\$name") -Recurse -Force -ErrorAction SilentlyContinue
    Pop-Location
}
