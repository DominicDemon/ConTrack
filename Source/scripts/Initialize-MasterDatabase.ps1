param(
    [string]$Server = 'localhost',
    [string]$Database = 'ContractManagement_Master'
)

$ErrorActionPreference = 'Stop'
if ($Database -notmatch '^[A-Za-z][A-Za-z0-9_]{0,127}$' -or $Database -in @('master', 'model', 'msdb', 'tempdb')) {
    throw 'Use a separate application database name.'
}

$scriptPath = Join-Path $PSScriptRoot '../Artifacts/Database/Master/MasterDB-Optimize.sql'
if (!(Test-Path -LiteralPath $scriptPath)) { throw "SQL source not found: $scriptPath" }

# Integrated authentication: no credentials are stored in this script.
$connection = [System.Data.SqlClient.SqlConnection]::new(
    "Server=$Server;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10")
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandText = 'SELECT DB_ID(@name)'
    [void]$command.Parameters.AddWithValue('@name', $Database)
    $exists = $command.ExecuteScalar()
    $command.Parameters.Clear()
    if ($exists -is [DBNull]) {
        $command.CommandText = "CREATE DATABASE [$Database]"
        [void]$command.ExecuteNonQuery()
    }
    $connection.ChangeDatabase($Database)
    $command.CommandText = 'SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0'
    if ([int]$command.ExecuteScalar() -gt 0) {
        throw 'Database already contains tables. Import stopped; this DDL is not an upgrade script.'
    }

    $transaction = $connection.BeginTransaction()
    try {
        $command.Transaction = $transaction
        $command.CommandTimeout = 120
        $sql = Get-Content -LiteralPath $scriptPath -Raw -Encoding UTF8
        foreach ($batch in [regex]::Split($sql, '(?im)^\s*GO\s*(?:--[^\r\n]*)?\r?$')) {
            if (![string]::IsNullOrWhiteSpace($batch)) {
                $command.CommandText = $batch
                [void]$command.ExecuteNonQuery()
            }
        }
        $command.CommandText = 'SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0'
        $count = [int]$command.ExecuteScalar()
        if ($count -ne 60) { throw "Expected 60 tables; found $count. Import rolled back." }
        $transaction.Commit()
        Write-Output "Imported $count tables into $Database on $Server. No sample data was inserted."
    } catch {
        $transaction.Rollback()
        throw
    } finally { $transaction.Dispose() }
} finally { $connection.Dispose() }
