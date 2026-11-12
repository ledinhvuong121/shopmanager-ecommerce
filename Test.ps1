$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot 'work/dotnet'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot 'work/packages'
dotnet test ShopManager.slnx --logger 'trx;LogFileName=business-tests.trx' --results-directory work/TestResults
if ($LASTEXITCODE -ne 0) { throw 'Kiểm thử thất bại.' }
