$ErrorActionPreference = 'Stop'
$credential = Import-Clixml -LiteralPath (Join-Path $PSScriptRoot 'work/local-access.clixml')
Write-Host 'Địa chỉ: http://localhost:5179'
Write-Host ('Tài khoản: ' + $credential.UserName)
Write-Host ('Mật khẩu đã lưu: ' + $credential.GetNetworkCredential().Password)
