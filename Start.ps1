param([switch]$Foreground)
$ErrorActionPreference = 'Stop'
$projectDirectory = Join-Path $PSScriptRoot 'src\ShopManager.Web'
$appPath = Join-Path $projectDirectory 'bin\Debug\net10.0\ShopManager.Web.exe'
$runtimeDirectory = Join-Path $PSScriptRoot 'work\runtime'
$statePath = Join-Path $runtimeDirectory 'server.json'
$baseUrl = 'http://localhost:5179'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot 'work\dotnet'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot 'work\packages'
New-Item -ItemType Directory -Path $runtimeDirectory -Force | Out-Null

# Never terminate or replace an unrelated process using the same port.
$listeners = @(Get-NetTCPConnection -LocalPort 5179 -State Listen -ErrorAction SilentlyContinue)
if ($listeners.Count -gt 0) {
    $owners = @($listeners | Select-Object -ExpandProperty OwningProcess -Unique)
    $existing = if ($owners.Count -eq 1) { Get-Process -Id $owners[0] -ErrorAction SilentlyContinue } else { $null }
    if ($existing -and $existing.Path -eq $appPath) {
        Write-Host ('ShopManager dang chay: ' + $baseUrl)
        Write-Host 'Khong khoi dong them phien thu hai.'
        return
    }
    throw 'Cong 5179 dang duoc mot tien trinh khac su dung. Khong tu dong dung tien trinh do.'
}

Push-Location -LiteralPath $PSScriptRoot
try {
    Write-Host 'Dang bien dich ShopManager...'
    & dotnet build (Join-Path $projectDirectory 'ShopManager.Web.csproj') --nologo /p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Bien dich that bai. Xem thong bao phia tren.' }
} finally {
    Pop-Location
}
if (-not (Test-Path -LiteralPath $appPath)) { throw ('Khong tim thay ung dung: ' + $appPath) }

$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
$env:ASPNETCORE_ENVIRONMENT = 'Development'
try {
    if ($Foreground) {
        Write-Host ('Chay trong cua so hien tai: ' + $baseUrl + ' (Ctrl+C de dung)')
        Push-Location -LiteralPath $projectDirectory
        try { & $appPath --urls $baseUrl } finally { Pop-Location }
        return
    }

    $stdoutPath = Join-Path $runtimeDirectory 'stdout.log'
    $stderrPath = Join-Path $runtimeDirectory 'stderr.log'
    $serverProcess = Start-Process -FilePath $appPath -ArgumentList @('--urls', $baseUrl) -WorkingDirectory $projectDirectory -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
    @{
        ProcessId = $serverProcess.Id
        StartedAtUtcTicks = $serverProcess.StartTime.ToUniversalTime().Ticks.ToString()
    } | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding UTF8

    $timer = [Diagnostics.Stopwatch]::StartNew()
    $ready = $false
    while ($timer.Elapsed.TotalSeconds -lt 25) {
        $serverProcess.Refresh()
        if ($serverProcess.HasExited) { break }
        try {
            $response = Invoke-WebRequest -Uri ($baseUrl + '/Account/Login') -UseBasicParsing -TimeoutSec 2
            if ($response.StatusCode -eq 200 -and $response.Content -match 'ShopManager') { $ready = $true; break }
        } catch { }
        Start-Sleep -Milliseconds 250
    }
    if (-not $ready) {
        # Stop only the process object this invocation created.
        $serverProcess.Refresh()
        if (-not $serverProcess.HasExited) { $serverProcess.Kill(); $serverProcess.WaitForExit() }
        Remove-Item -LiteralPath $statePath -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath -Tail 25 }
        throw ('Ung dung chua san sang. Kiem tra log tai ' + $runtimeDirectory)
    }
    Write-Host ''
    Write-Host ('DA SAN SANG: ' + $baseUrl) -ForegroundColor Green
    Write-Host 'Ung dung dang chay nen. Ban co the dong cua so PowerShell.'
    Write-Host 'Dung ung dung: .\Stop.ps1'
    Write-Host 'Xem log: Get-Content .\work\runtime\stdout.log -Tail 30'
} finally {
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
}
