param()
$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path $PSScriptRoot -Parent
$localDirectory = Join-Path $sourceRoot '.local'
$accountPath = Join-Path $localDirectory 'internal-accounts.json'
New-Item -ItemType Directory -Force -Path $localDirectory | Out-Null
if (!(Test-Path -LiteralPath $accountPath)) {
    $accounts = @{
        sale = @{ username = 'sale.local'; password = 'Aa1!' + [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)) }
        office = @{ username = 'office.local'; password = 'Aa1!' + [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)) }
    }
    $accounts | ConvertTo-Json | Set-Content -LiteralPath $accountPath -Encoding utf8
}
$accounts = Get-Content -LiteralPath $accountPath -Raw | ConvertFrom-Json
$previousMode = $env:ApplicationMode
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
try {
    $env:ApplicationMode = 'Internal'
    $env:InternalBootstrap__SalePassword = $accounts.sale.password
    $env:InternalBootstrap__AdminPassword = $accounts.office.password
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    dotnet run --project (Join-Path $sourceRoot 'Backend/ContractManagement.API') --no-launch-profile -- --seed-internal
    if ($LASTEXITCODE -ne 0) { throw 'Internal initialization failed.' }
    Write-Output "Local account details: $accountPath (ignored by Git). Existing passwords are not reset."
} finally {
    $env:ApplicationMode = $previousMode
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
    Remove-Item Env:InternalBootstrap__SalePassword -ErrorAction SilentlyContinue
    Remove-Item Env:InternalBootstrap__AdminPassword -ErrorAction SilentlyContinue
}
