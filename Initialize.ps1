param(
    [string]$AdminEmail = 'admin@shopmanager.local',
    [switch]$SampleData,
    [switch]$MigrateOnly
)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot 'work/dotnet'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot 'work/packages'
dotnet restore ShopManager.slnx
if ($LASTEXITCODE -ne 0) { throw 'Khôi phục thư viện thất bại.' }
if ($MigrateOnly) {
    if ($SampleData) { throw 'Không dùng -SampleData cùng -MigrateOnly.' }
    & dotnet run --project src/ShopManager.Web --no-launch-profile -- --migrate-only
    if ($LASTEXITCODE -ne 0) { throw 'Cập nhật database thất bại.' }
    return
}
$securePassword = Read-Host 'Mật khẩu quản lý (10+ ký tự, gồm hoa/thường/số/ký tự đặc biệt)' -AsSecureString
$env:Seed__AdminEmail = $AdminEmail
$env:Seed__AdminPassword = [System.Net.NetworkCredential]::new('', $securePassword).Password
try {
    $arguments = @('run', '--project', 'src/ShopManager.Web', '--no-launch-profile', '--', '--initialize')
    if ($SampleData) { $arguments += '--sample-data' }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Khởi tạo thất bại. Kiểm tra kết nối SQL Server và mật khẩu.' }
} finally {
    Remove-Item Env:Seed__AdminPassword -ErrorAction SilentlyContinue
    Remove-Item Env:Seed__AdminEmail -ErrorAction SilentlyContinue
}
