$host.UI.RawUI.WindowTitle = "Aider"
$env:PYTHONUTF8 = "1"
$env:PYTHONIOENCODING = "utf-8"
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
Write-Host "========================================================" -ForegroundColor Green
Write-Host ""
Write-Host " Tips: /ask, /add Assets, /undo, /exit" -ForegroundColor DarkGray
Write-Host ""
& "C:\Users\G2546\AppData\Local\Programs\Python\Python312\Scripts\aider.exe" $args
