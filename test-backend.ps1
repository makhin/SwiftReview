param([string[]]$TestArguments = @())
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($env:ORP_TEST_SQL_SERVER)) {
    throw 'Set ORP_TEST_SQL_SERVER to an external SQL Server connection with CREATE/DROP DATABASE permissions.'
}
$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location (Join-Path $repositoryRoot 'backend')
try {
    & dotnet test ORP.sln @TestArguments
    $testExitCode = $LASTEXITCODE
}
finally { Pop-Location }
exit $testExitCode
