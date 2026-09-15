$ErrorActionPreference = 'Stop'
$syncScript = Join-Path $PSScriptRoot 'Sync-Folders.ps1'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('sync-folders-test-' + [guid]::NewGuid())
$source = Join-Path $testRoot 'source folder'
$dest = Join-Path $testRoot 'destination folder'
$nativeRobocopy = Get-Command robocopy.exe -CommandType Application -ErrorAction SilentlyContinue
$passed = 0
$robocopyTestState = @{ Arguments = @(); ExitCode = 0; UseNativeProcess = $false }
$testPowerShellName = if ($PSVersionTable.PSVersion.Major -ge 6) { 'pwsh' } else { 'powershell.exe' }
$testPowerShell = (Get-Command $testPowerShellName -CommandType Application -ErrorAction Stop).Source

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Test-Case([string]$Name, [scriptblock]$Body) {
    & $Body
    $script:passed++
    Write-Host "PASS: $Name"
}

function Assert-Rejected([scriptblock]$Action, [string]$ExpectedMessage) {
    $failure = $null
    try { & $Action } catch { $failure = $_ }
    Assert-True ($null -ne $failure) 'Expected the script to reject the operation.'
    Assert-True ($failure.Exception.Message -like $ExpectedMessage) "Unexpected error: $failure"
}

# Capture invocation without performing a real sync during wrapper tests.
function robocopy.exe {
    $robocopyTestState.Arguments = @($args)
    if ($robocopyTestState.UseNativeProcess) {
        # Exercise native exit handling; a function assigning LASTEXITCODE cannot do that.
        & $testPowerShell -NoLogo -NoProfile -Command "exit $($robocopyTestState.ExitCode)"
        Assert-True ($LASTEXITCODE -eq $robocopyTestState.ExitCode) 'Native test process returned an unexpected exit code.'
    }
    $global:LASTEXITCODE = $robocopyTestState.ExitCode
}

try {
    New-Item -ItemType Directory -Path $source, $dest | Out-Null
    $robocopyTestState.ExitCode = 0

    Test-Case 'Passes paths with spaces and the required sync options' {
        & $syncScript -Source $source -Dest $dest
        $expected = @($source, $dest, '/E', '/XO', '/XC', '/COPY:DAT', '/XJ', '/R:2', '/W:1', '/PURGE')
        Assert-True (($robocopyTestState.Arguments -join '|') -eq ($expected -join '|')) "Incorrect Robocopy arguments: $($robocopyTestState.Arguments -join '|')"
    }

    Test-Case 'Preview adds the list-only option' {
        & $syncScript -Source $source -Dest $dest -Preview
        Assert-True ($robocopyTestState.Arguments -contains '/L') 'Preview did not enable list-only mode.'
    }

    Test-Case 'NoDelete preserves copy options and disables purging' {
        foreach ($preview in @($false, $true)) {
            & $syncScript -Source $source -Dest $dest -NoDelete -Preview:$preview
            $expected = @($source, $dest, '/E', '/XO', '/XC', '/COPY:DAT', '/XJ', '/R:2', '/W:1')
            if ($preview) { $expected += '/L' }
            Assert-True (($robocopyTestState.Arguments -join '|') -eq ($expected -join '|')) 'Incorrect NoDelete options.'
        }
    }

    Test-Case 'Allows a destination that does not exist yet' {
        & $syncScript -Source $source -Dest (Join-Path $testRoot 'new destination')
    }

    Test-Case 'Resolves relative paths with spaces from the caller directory' {
        Push-Location -LiteralPath $testRoot
        try {
            & $syncScript -Source './source folder' -Dest './destination folder'
            Assert-True ($robocopyTestState.Arguments[0] -eq $source) 'Relative source resolved incorrectly.'
            Assert-True ($robocopyTestState.Arguments[1] -eq $dest) 'Relative destination resolved incorrectly.'
        } finally { Pop-Location }
    }

    Test-Case 'Resolves dot and parent segments with a missing relative destination' {
        Push-Location -LiteralPath $source
        try {
            & $syncScript -Source '../source folder/.' -Dest '../new destination/child' -Preview
            Assert-True ($robocopyTestState.Arguments[0].TrimEnd([IO.Path]::DirectorySeparatorChar) -eq $source) 'Source parent segments resolved incorrectly.'
            Assert-True ($robocopyTestState.Arguments[1] -eq (Join-Path $testRoot 'new destination/child')) 'Missing relative destination resolved incorrectly.'
            Assert-True (-not (Test-Path -LiteralPath '../new destination')) 'Preview created the destination.'
        } finally { Pop-Location }
    }

    Test-Case 'Supports dot as source with a sibling destination' {
        Push-Location -LiteralPath $source
        try {
            & $syncScript -Source '.' -Dest '../destination folder'
            Assert-True ($robocopyTestState.Arguments[0] -eq $source) 'Dot source resolved incorrectly.'
            Assert-True ($robocopyTestState.Arguments[1] -eq $dest) 'Sibling destination resolved incorrectly.'
        } finally { Pop-Location }
    }

    Test-Case 'Rejects overlapping relative paths after normalization' {
        Push-Location -LiteralPath $testRoot
        try {
            Assert-Rejected { & $syncScript -Source './source folder' -Dest './destination folder/../source folder' } 'Source and Dest must be different*'
            Assert-Rejected { & $syncScript -Source './source folder' -Dest './source folder/child' } 'Source and Dest must be different*'
            Assert-Rejected { & $syncScript -Source './source folder' -Dest '.' } 'Source and Dest must be different*'
        } finally { Pop-Location }
    }

    # Junctions on Windows do not require the symbolic-link creation privilege.
    $linkType = if ([IO.Path]::DirectorySeparatorChar -eq '\') { 'Junction' } else { 'SymbolicLink' }
    $alias = Join-Path $testRoot 'alias'
    New-Item -ItemType $linkType -Path $alias -Target $testRoot | Out-Null
    try {
        Test-Case 'Rejects an aliased destination containing source' {
            $robocopyTestState.Arguments = @()
            Assert-Rejected { & $syncScript -Source $source -Dest $alias } 'Paths must not contain symbolic links or junctions:*'
            Assert-True ($robocopyTestState.Arguments.Count -eq 0) 'Robocopy was invoked for an unsafe path.'
        }

        Test-Case 'Rejects a source with an aliased ancestor' {
            Assert-Rejected { & $syncScript -Source (Join-Path $alias 'source folder') -Dest $dest } 'Paths must not contain symbolic links or junctions:*'
        }

        Test-Case 'Rejects missing destinations beneath an aliased ancestor' {
            Assert-Rejected { & $syncScript -Source $source -Dest (Join-Path $alias 'missing/child') } 'Paths must not contain symbolic links or junctions:*'
        }
    } finally {
        # Remove the link itself, never recurse into its target.
        (Get-Item -LiteralPath $alias -Force).Delete()
    }

    Test-Case 'Rejects missing source' {
        Assert-Rejected { & $syncScript -Source (Join-Path $testRoot 'missing') -Dest $dest } 'Source directory not found:*'
    }

    Test-Case 'Rejects a destination file' {
        $file = Join-Path $testRoot 'file.txt'
        Set-Content -LiteralPath $file -Value 'test'
        Assert-Rejected { & $syncScript -Source $source -Dest $file } 'Destination path points to a file:*'
    }

    Test-Case 'Rejects identical directories' {
        Assert-Rejected { & $syncScript -Source $source -Dest $source } 'Source and Dest must be different*'
    }

    Test-Case 'Rejects a destination inside source' {
        Assert-Rejected { & $syncScript -Source $source -Dest (Join-Path $source 'child') } 'Source and Dest must be different*'
    }

    Test-Case 'Rejects a source inside destination' {
        Assert-Rejected { & $syncScript -Source $source -Dest $testRoot } 'Source and Dest must be different*'
    }

    Test-Case 'Accepts all successful Robocopy exit codes' {
        foreach ($code in 0..7) {
            $robocopyTestState.ExitCode = $code
            & $syncScript -Source $source -Dest $dest
        }
    }

    Test-Case 'Reports Robocopy failures' {
        foreach ($code in @(8, 16)) {
            $robocopyTestState.ExitCode = $code
            Assert-Rejected { & $syncScript -Source $source -Dest $dest } "Robocopy failed with exit code: $code"
        }
    }

    Test-Case 'Handles native success and failure codes with caller error preference enabled' {
        $PSNativeCommandUseErrorActionPreference = $true
        $robocopyTestState.UseNativeProcess = $true
        try {
            foreach ($code in @(0, 1, 7)) {
                $robocopyTestState.ExitCode = $code
                & $syncScript -Source $source -Dest $dest
            }
            $robocopyTestState.ExitCode = 8
            Assert-Rejected { & $syncScript -Source $source -Dest $dest } 'Robocopy failed with exit code: 8'
            Assert-True $PSNativeCommandUseErrorActionPreference 'The script changed the caller preference.'
        } finally { $robocopyTestState.UseNativeProcess = $false }
    }

    Remove-Item Function:\robocopy.exe

    if ($nativeRobocopy) {
        Test-Case 'Real sync adds, updates, preserves, deletes, and supports preview' {
            $older = [datetime]::UtcNow.AddDays(-2)
            $newer = $older.AddDays(1)
            foreach ($name in @('update.txt', 'preserve.txt', 'equal.txt')) {
                Set-Content -LiteralPath (Join-Path $source $name) -Value 'source content'
                Set-Content -LiteralPath (Join-Path $dest $name) -Value 'destination content of a different size'
                (Get-Item -LiteralPath (Join-Path $source $name)).LastWriteTimeUtc = $newer
                (Get-Item -LiteralPath (Join-Path $dest $name)).LastWriteTimeUtc = $newer
            }
            (Get-Item -LiteralPath (Join-Path $dest 'update.txt')).LastWriteTimeUtc = $older
            (Get-Item -LiteralPath (Join-Path $source 'preserve.txt')).LastWriteTimeUtc = $older
            New-Item -ItemType Directory -Path (Join-Path $source 'nested'), (Join-Path $source 'empty'), (Join-Path $dest 'extra') | Out-Null
            Set-Content -LiteralPath (Join-Path $source 'nested/new.txt') -Value 'new content'
            Set-Content -LiteralPath (Join-Path $dest 'extra/obsolete.txt') -Value 'obsolete'
            Set-Content -LiteralPath (Join-Path $dest 'obsolete.txt') -Value 'obsolete'

            & $syncScript -Source $source -Dest $dest -Preview | Out-Null
            Assert-True (-not (Test-Path -LiteralPath (Join-Path $dest 'nested'))) 'Preview copied files.'
            Assert-True (Test-Path -LiteralPath (Join-Path $dest 'extra/obsolete.txt')) 'Preview deleted files.'
            Assert-True ((Get-Content -LiteralPath (Join-Path $dest 'update.txt')) -eq 'destination content of a different size') 'Preview updated files.'

            & $syncScript -Source $source -Dest $dest -NoDelete | Out-Null
            Assert-True ((Get-Content -LiteralPath (Join-Path $dest 'update.txt')) -eq 'source content') 'NoDelete did not update a newer file.'
            Assert-True ((Get-Content -LiteralPath (Join-Path $dest 'nested/new.txt')) -eq 'new content') 'NoDelete did not add a new file.'
            Assert-True (Test-Path -LiteralPath (Join-Path $dest 'extra/obsolete.txt')) 'NoDelete removed a destination-only directory or file.'
            Assert-True (Test-Path -LiteralPath (Join-Path $dest 'obsolete.txt')) 'NoDelete removed a destination-only file.'

            & $syncScript -Source $source -Dest $dest | Out-Null
            Assert-True ((Get-Content -LiteralPath (Join-Path $dest 'update.txt')) -eq 'source content') 'Newer source file was not copied.'
            foreach ($name in @('preserve.txt', 'equal.txt')) {
                Assert-True ((Get-Content -LiteralPath (Join-Path $dest $name)) -eq 'destination content of a different size') "Destination file was overwritten: $name"
            }
            Assert-True ((Get-Content -LiteralPath (Join-Path $dest 'nested/new.txt')) -eq 'new content') 'New nested file was not copied.'
            Assert-True (Test-Path -LiteralPath (Join-Path $dest 'empty') -PathType Container) 'Empty directory was not copied.'
            Assert-True (-not (Test-Path -LiteralPath (Join-Path $dest 'extra'))) 'Extra directory was not deleted.'
            Assert-True (-not (Test-Path -LiteralPath (Join-Path $dest 'obsolete.txt'))) 'Extra file was not deleted.'
            Assert-True (Test-Path -LiteralPath (Join-Path $source 'nested/new.txt')) 'Source file was removed.'
        }
    } else {
        Write-Host 'SKIP: Real file-sync integration test requires Windows robocopy.exe.'
    }

    Write-Host "$passed tests passed."
} finally {
    Remove-Item Function:\robocopy.exe -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
