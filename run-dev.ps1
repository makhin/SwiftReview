$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$backendProcess = $null
$frontendProcess = $null
$exitCode = 0

try {
    $backendProcess = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList "run", "--project", "backend/src/ORP.Api" `
        -WorkingDirectory $repositoryRoot `
        -NoNewWindow `
        -PassThru

    $frontendProcess = Start-Process `
        -FilePath "npm.cmd" `
        -ArgumentList "--prefix", "frontend", "run", "dev" `
        -WorkingDirectory $repositoryRoot `
        -NoNewWindow `
        -PassThru

    while (-not $backendProcess.HasExited -and -not $frontendProcess.HasExited) {
        Start-Sleep -Milliseconds 500
        $backendProcess.Refresh()
        $frontendProcess.Refresh()
    }

    if ($backendProcess.HasExited) {
        $exitCode = $backendProcess.ExitCode
    }
    else {
        $exitCode = $frontendProcess.ExitCode
    }
}
finally {
    foreach ($process in @($backendProcess, $frontendProcess)) {
        if ($null -ne $process -and -not $process.HasExited) {
            & taskkill.exe /PID $process.Id /T /F *> $null
        }
    }
}

exit $exitCode
