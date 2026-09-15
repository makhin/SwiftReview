#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$copyScript = Join-Path $PSScriptRoot 'Copy-GitCommit.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('git-commit-test-' + [guid]::NewGuid())
$repo = Join-Path $root 'source repo'
$passed = 0

function Invoke-FixtureGit([string[]]$Arguments) {
    $result = & git -C $repo @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Fixture Git command failed: $Arguments" }
    return $result
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-Rejected([scriptblock]$Action, [string]$Message) {
    $failure = $null
    try { & $Action } catch { $failure = $_ }
    Assert-True ($null -ne $failure -and $failure.Exception.Message -like $Message) "Expected '$Message'; received '$failure'."
}

function Write-File([string]$Path, [string]$Text) {
    $null = [IO.Directory]::CreateDirectory((Split-Path -Path $Path -Parent))
    [IO.File]::WriteAllText($Path, $Text)
}

function Test-Case([string]$Name, [scriptblock]$Body) {
    $dest = Join-Path $root ([guid]::NewGuid().ToString())
    & $Body
    $script:passed++
    Write-Host "PASS: $Name"
}

try {
    $null = [IO.Directory]::CreateDirectory($repo)
    Invoke-FixtureGit @('init', '-b', 'main') | Out-Null
    Invoke-FixtureGit @('config', 'user.name', 'Script Test')
    Invoke-FixtureGit @('config', 'user.email', 'script-test@example.invalid')
    Invoke-FixtureGit @('config', 'core.autocrlf', 'false')
    Invoke-FixtureGit @('config', 'commit.gpgsign', 'false')
    Write-File (Join-Path $repo 'updated.txt') 'base'
    Write-File (Join-Path $repo 'deleted.txt') 'delete me'
    Write-File (Join-Path $repo 'old name.txt') 'rename me'
    Write-File (Join-Path $repo 'untouched.txt') 'base untouched'
    Invoke-FixtureGit @('add', '--all')
    Invoke-FixtureGit @('commit', '-m', 'Initial') | Out-Null
    $initial = Invoke-FixtureGit @('rev-parse', 'HEAD')

    Write-File (Join-Path $repo 'updated.txt') 'selected commit'
    Remove-Item -LiteralPath (Join-Path $repo 'deleted.txt')
    Invoke-FixtureGit @('mv', 'old name.txt', 'renamed file.txt')
    $specialName = "nested folder/file [$([char]0xE9)].txt"
    Write-File (Join-Path $repo $specialName) 'nested content'
    $binary = [byte[]](0..255)
    [IO.File]::WriteAllBytes((Join-Path $repo 'binary.bin'), $binary)
    Invoke-FixtureGit @('add', '--all')
    Invoke-FixtureGit @('commit', '-m', 'Selected changes') | Out-Null
    $selected = Invoke-FixtureGit @('rev-parse', 'HEAD')

    Test-Case 'Default HEAD transfers additions, modifications, deletions, and renames' {
        Write-File (Join-Path $dest 'updated.txt') 'destination edit'
        Write-File (Join-Path $dest 'deleted.txt') 'delete me'
        Write-File (Join-Path $dest 'old name.txt') 'rename me'
        Write-File (Join-Path $dest 'unrelated.txt') 'keep me'
        & $copyScript -Source $repo -Dest $dest
        Assert-True ([IO.File]::ReadAllText((Join-Path $dest 'updated.txt')) -eq 'selected commit') 'Modified file differs.'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $dest 'deleted.txt'))) 'Deleted file remains.'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $dest 'old name.txt'))) 'Rename source remains.'
        Assert-True ([IO.File]::ReadAllText((Join-Path $dest 'renamed file.txt')) -eq 'rename me') 'Rename target differs.'
        Assert-True ([IO.File]::ReadAllText((Join-Path $dest $specialName)) -eq 'nested content') 'Special path differs.'
        Assert-True ([Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $dest 'binary.bin'))) -eq [Convert]::ToBase64String($binary)) 'Binary bytes differ.'
        Assert-True ([IO.File]::ReadAllText((Join-Path $dest 'unrelated.txt')) -eq 'keep me') 'Unrelated file changed.'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $dest 'untouched.txt'))) 'An unchanged source file was copied.'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $dest '.git'))) 'Git metadata was copied.'
    }

    Test-Case 'Preview leaves existing files and missing destinations unchanged' {
        & $copyScript -Source $repo -Dest $dest -Preview
        Assert-True (-not (Test-Path -LiteralPath $dest)) 'Preview created the destination.'
        Write-File (Join-Path $dest 'deleted.txt') 'keep for preview'
        & $copyScript -Source $repo -Dest $dest -Preview
        Assert-True ([IO.File]::ReadAllText((Join-Path $dest 'deleted.txt')) -eq 'keep for preview') 'Preview deleted a file.'
        Assert-True (@(Get-ChildItem -LiteralPath $dest).Count -eq 1) 'Preview added files.'
    }

    Test-Case 'NoDelete preserves deleted and renamed source paths' {
        Write-File (Join-Path $dest 'deleted.txt') 'keep deleted'
        Write-File (Join-Path $dest 'old name.txt') 'keep old name'
        & $copyScript -Source $repo -Dest $dest -NoDelete
        Assert-True ([IO.File]::ReadAllText((Join-Path $dest 'deleted.txt')) -eq 'keep deleted') 'NoDelete deleted a file.'
        Assert-True (Test-Path -LiteralPath (Join-Path $dest 'old name.txt')) 'NoDelete removed an old name.'
        Assert-True (Test-Path -LiteralPath (Join-Path $dest 'renamed file.txt')) 'NoDelete skipped new files.'
    }

    Write-File (Join-Path $repo 'updated.txt') 'later commit'
    Invoke-FixtureGit @('add', '--all')
    Invoke-FixtureGit @('commit', '-m', 'Later') | Out-Null
    Write-File (Join-Path $repo 'updated.txt') 'uncommitted edit'
    Remove-Item -LiteralPath (Join-Path $repo 'binary.bin')
    $dirtyState = (Invoke-FixtureGit @('status', '--porcelain')) -join "`n"

    Test-Case 'Selected older commit ignores later commits and dirty working-tree files' {
        & $copyScript -Source $repo -Dest $dest -Commit $selected
        Assert-True ([IO.File]::ReadAllText((Join-Path $dest 'updated.txt')) -eq 'selected commit') 'Copied working-tree or later contents.'
        Assert-True ([IO.File]::ReadAllBytes((Join-Path $dest 'binary.bin')).Length -eq 256) 'Missing working-tree file was not recovered from Git.'
        Assert-True (((Invoke-FixtureGit @('status', '--porcelain')) -join "`n") -eq $dirtyState) 'Source working tree changed.'
    }

    Test-Case 'Initial commit copies its entire tree' {
        & $copyScript -Source $repo -Dest $dest -Commit $initial
        Assert-True ([IO.File]::ReadAllText((Join-Path $dest 'updated.txt')) -eq 'base') 'Initial contents differ.'
        Assert-True (@(Get-ChildItem -LiteralPath $dest -File -Recurse).Count -eq 4) 'Initial tree is incomplete.'
    }

    Test-Case 'Relative paths and revision expressions work' {
        Push-Location -LiteralPath $root
        try { & $copyScript -Source './source repo' -Dest (Split-Path -Path $dest -Leaf) -Commit 'HEAD~1' }
        finally { Pop-Location }
        Assert-True ([IO.File]::ReadAllText((Join-Path $dest 'updated.txt')) -eq 'selected commit') 'Relative arguments resolved incorrectly.'
    }

    Test-Case 'Repository subdirectory still uses repository-root paths' {
        & $copyScript -Source (Join-Path $repo 'nested folder') -Dest $dest -Commit $selected
        Assert-True (Test-Path -LiteralPath (Join-Path $dest $specialName)) 'Paths were relative to the source subdirectory.'
    }

    Test-Case 'Invalid commit fails before changing destination' {
        Assert-Rejected { & $copyScript -Source $repo -Dest $dest -Commit '--invalid' } 'Git failed*'
        Assert-True (-not (Test-Path -LiteralPath $dest)) 'Invalid commit created the destination.'
    }

    Test-Case 'Overlapping source and destination are rejected' {
        Assert-Rejected { & $copyScript -Source $repo -Dest (Join-Path $repo 'output') } 'Source and Dest must be different*'
        Assert-Rejected { & $copyScript -Source $repo -Dest $root } 'Source and Dest must be different*'
    }

    Test-Case 'Destination conflicts fail before applying any files' {
        $null = [IO.Directory]::CreateDirectory((Join-Path $dest 'updated.txt'))
        Assert-Rejected { & $copyScript -Source $repo -Dest $dest -Commit $selected } 'Destination directory conflicts*'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $dest 'binary.bin'))) 'A file was copied before validation finished.'
    }

    Test-Case 'Destination links cannot redirect writes outside the destination' {
        $outside = Join-Path $root 'outside'
        $null = [IO.Directory]::CreateDirectory($outside)
        $null = [IO.Directory]::CreateDirectory($dest)
        $link = Join-Path $dest 'nested folder'
        $linkType = if ($IsWindows) { 'Junction' } else { 'SymbolicLink' }
        New-Item -ItemType $linkType -Path $link -Target $outside | Out-Null
        try {
            Assert-Rejected { & $copyScript -Source $repo -Dest $dest -Commit $selected } 'Paths must not contain symbolic links*'
            Assert-True (@(Get-ChildItem -LiteralPath $outside).Count -eq 0) 'Wrote outside destination.'
        } finally { (Get-Item -LiteralPath $link -Force).Delete() }
    }

    # Reset only this disposable fixture to construct a merge commit.
    Invoke-FixtureGit @('reset', '--hard', 'HEAD') | Out-Null
    Invoke-FixtureGit @('checkout', '-b', 'feature') | Out-Null
    Write-File (Join-Path $repo 'feature.txt') 'feature content'
    Invoke-FixtureGit @('add', '--all')
    Invoke-FixtureGit @('commit', '-m', 'Feature') | Out-Null
    Invoke-FixtureGit @('checkout', 'main') | Out-Null
    Write-File (Join-Path $repo 'main.txt') 'main content'
    Invoke-FixtureGit @('add', '--all')
    Invoke-FixtureGit @('commit', '-m', 'Main') | Out-Null
    Invoke-FixtureGit @('merge', '--no-ff', 'feature', '-m', 'Merge feature') | Out-Null

    Test-Case 'Merge commit compares against its first parent' {
        & $copyScript -Source $repo -Dest $dest
        Assert-True ([IO.File]::ReadAllText((Join-Path $dest 'feature.txt')) -eq 'feature content') 'Merge addition missing.'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $dest 'main.txt'))) 'Included first-parent changes.'
    }

    Invoke-FixtureGit @('commit', '--allow-empty', '-m', 'Empty') | Out-Null
    Test-Case 'Empty commit is a no-op' {
        & $copyScript -Source $repo -Dest $dest
        Assert-True (-not (Test-Path -LiteralPath $dest)) 'Empty commit created a destination.'
    }

    # Index entries let these tests run without requiring symlink privileges.
    $linkBlob = Invoke-FixtureGit @('rev-parse', 'HEAD:feature.txt')
    Invoke-FixtureGit @('update-index', '--add', '--cacheinfo', "120000,$linkBlob,unsupported-link")
    Invoke-FixtureGit @('commit', '-m', 'Symbolic link') | Out-Null
    Test-Case 'Committed symbolic links are rejected before changing destination' {
        Assert-Rejected { & $copyScript -Source $repo -Dest $dest } 'Only regular files are supported*'
        Assert-True (-not (Test-Path -LiteralPath $dest)) 'Unsupported link created a destination.'
    }

    Invoke-FixtureGit @('update-index', '--add', '--cacheinfo', "160000,$selected,submodule")
    Invoke-FixtureGit @('commit', '-m', 'Submodule') | Out-Null
    Test-Case 'Submodules are rejected before changing destination' {
        Assert-Rejected { & $copyScript -Source $repo -Dest $dest } 'Only regular files are supported*'
        Assert-True (-not (Test-Path -LiteralPath $dest)) 'Unsupported submodule created a destination.'
    }

    Write-Host "$passed tests passed."
} finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
