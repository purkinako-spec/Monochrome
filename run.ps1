$host.UI.RawUI.WindowTitle = "Aider"
$env:PYTHONUTF8 = "1"
$env:PYTHONIOENCODING = "utf-8"
$env:PYTHONUNBUFFERED = "1"
$keysFile = "$env:USERPROFILE\.openrouter_keys.json"
if (Test-Path $keysFile) {
    $data = Get-Content $keysFile -Raw | ConvertFrom-Json
    $cur  = $data.accounts[$data.currentIndex]
    $env:OPENROUTER_API_KEY = $cur.key
} else {
    $cur = [PSCustomObject]@{ id="?"; name="unknown"; email="unknown" }
}
$confPath = "$env:USERPROFILE\.aider.conf.yml"
$currentModel = "(unknown)"
if (Test-Path $confPath) {
    $line = Get-Content $confPath | Where-Object { $_ -match "^model:" } | Select-Object -First 1
    if ($line) { $currentModel = ($line -replace "^model:\s*", "").Trim() }
}
Write-Host "========================================================" -ForegroundColor Green
Write-Host " [Aider] Account : [$($cur.id)] $($cur.name) ($($cur.email))" -ForegroundColor Cyan
Write-Host " [Aider] Model   : $currentModel" -ForegroundColor Magenta
Write-Host " [Aider] LogMode : Auto-tracking AiderLogs/unity_errors.log" -ForegroundColor Yellow
Write-Host "========================================================" -ForegroundColor Green
Write-Host ""
Write-Host " Tips:" -ForegroundColor DarkGray
Write-Host "   Unity Error -> Tell Aider: Check unity_errors.log and fix" -ForegroundColor Cyan
Write-Host "   /ask [question] -> Ask without editing files" -ForegroundColor DarkGray
Write-Host "   /add Assets     -> Add all C# scripts" -ForegroundColor DarkGray
Write-Host "   /undo           -> Revert last AI change" -ForegroundColor DarkGray
Write-Host "   /exit           -> Exit" -ForegroundColor DarkGray
Write-Host ""
$targetLog = "AiderLogs\unity_errors.log"
if (-not (Test-Path "AiderLogs")) { New-Item -ItemType Directory -Path "AiderLogs" -Force | Out-Null }
if (-not (Test-Path $targetLog)) { New-Item -ItemType File -Path $targetLog -Force | Out-Null }
$aiderCmd = (Get-Command "aider" -ErrorAction SilentlyContinue).Source
if (-not $aiderCmd) { $aiderCmd = "$env:LOCALAPPDATA\Programs\Python\Python312\Scripts\aider.exe" }
if (-not (Test-Path $aiderCmd)) { $aiderCmd = "aider" }
& $aiderCmd --read $targetLog $args
