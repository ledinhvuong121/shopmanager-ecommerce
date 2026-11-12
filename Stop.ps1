$ErrorActionPreference = 'Stop'
$statePath = Join-Path $PSScriptRoot 'work\runtime\server.json'
$appPath = Join-Path $PSScriptRoot 'src\ShopManager.Web\bin\Debug\net10.0\ShopManager.Web.exe'
if (-not (Test-Path -LiteralPath $statePath)) {
    Write-Host 'Khong co phien chay nen duoc Start.ps1 quan ly.'
    return
}
$state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
$serverProcess = Get-Process -Id ([int]$state.ProcessId) -ErrorAction SilentlyContinue
if (-not $serverProcess) {
    Remove-Item -LiteralPath $statePath
    Write-Host 'Ung dung da dung truoc do.'
    return
}
# Check path and creation time: a recycled PID must never kill another process.
if ($serverProcess.Path -ne $appPath -or $serverProcess.StartTime.ToUniversalTime().Ticks.ToString() -ne [string]$state.StartedAtUtcTicks) {
    throw 'Tien trinh khong khop phien da khoi dong. Khong dung tien trinh nay.'
}
Stop-Process -Id $serverProcess.Id -Force
$serverProcess.WaitForExit()
Remove-Item -LiteralPath $statePath
Write-Host 'Da dung ShopManager. Du lieu SQL Server van duoc giu nguyen.'
