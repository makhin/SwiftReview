param([string[]]$TestArguments = @())
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location (Join-Path $repositoryRoot 'backend')
try {
    & dotnet test ORP.sln @TestArguments
    $testExitCode = $LASTEXITCODE
}
finally { Pop-Location }
exit $testExitCode
