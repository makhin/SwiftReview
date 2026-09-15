#Requires -Version 5.1
<#
.SYNOPSIS
Copies the files changed by one Git commit into a destination directory.
.DESCRIPTION
Source is a working Git repository; Dest does not need to be a repository.
Commit defaults to HEAD. Merge commits are compared with their first parent;
an initial commit adds all its files. Paths are relative to the repository root.
Changed files are replaced with the exact committed bytes, not working-tree
contents or a patch merged with destination edits. Git filters (including LFS
downloads) and file permissions are not applied. Unrelated files are preserved.
Renames are handled as deletion plus addition. Use -NoDelete to skip deletions.
Relative arguments resolve from the caller's current PowerShell directory.
Symbolic links, junctions, submodules, and destination file/directory conflicts
are rejected before applying changes. Use -Preview to print the plan only.
.EXAMPLE
powershell.exe -NoProfile -File scripts/Copy-GitCommit.ps1 -Source ../repo -Dest ../output -Preview
.EXAMPLE
powershell.exe -NoProfile -File scripts/Copy-GitCommit.ps1 -Source ../repo -Dest ../output -Commit HEAD~2
#>
param(
    [Parameter(Mandatory)] [string]$Source,
    [Parameter(Mandatory)] [string]$Dest,
    [string]$Commit = 'HEAD',
    [switch]$Preview,
    [switch]$NoDelete
)

$ErrorActionPreference = 'Stop'
$git = (Get-Command git -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source

function ConvertTo-NativeArgument([string]$Value) {
    # .NET Framework lacks ArgumentList. Quote for the Windows C runtime:
    # double backslashes before quotes and before the closing delimiter.
    $escaped = [regex]::Replace($Value, '(\\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
    return '"' + $escaped + '"'
}

function Invoke-Git([string[]]$GitArguments, [string]$OutputFile) {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $git
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardOutputEncoding = [Text.UTF8Encoding]::new($false, $true)
    $start.Arguments = ((@('--no-replace-objects', '-C', $Source) + $GitArguments |
        ForEach-Object { ConvertTo-NativeArgument $_ }) -join ' ')
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        $null = $process.Start()
        $errors = $process.StandardError.ReadToEndAsync()
        if ($OutputFile) {
            $stream = [IO.File]::Create($OutputFile)
            try { $process.StandardOutput.BaseStream.CopyTo($stream) } finally { $stream.Dispose() }
        } else {
            $output = $process.StandardOutput.ReadToEnd()
        }
        $process.WaitForExit()
        $errorText = $errors.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw "Git failed ($($process.ExitCode)): $errorText" }
        if (-not $OutputFile) { return $output }
    } finally { $process.Dispose() }
}

function Assert-NoLinks([string]$Path) {
    while ($Path) {
        if (Test-Path -LiteralPath $Path) {
            $item = Get-Item -LiteralPath $Path -Force
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Paths must not contain symbolic links or junctions: $Path"
            }
        }
        $Path = Split-Path -Path $Path -Parent
    }
}

if (-not (Test-Path -LiteralPath $Source -PathType Container)) {
    throw "Source directory not found: $Source"
}
$Source = (Get-Item -LiteralPath $Source -Force).FullName
Assert-NoLinks $Source
$Source = [IO.Path]::GetFullPath((Invoke-Git @('rev-parse', '--show-toplevel')).TrimEnd("`r", "`n"))
$Dest = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Dest)
Assert-NoLinks $Source
Assert-NoLinks $Dest
if (Test-Path -LiteralPath $Dest -PathType Leaf) { throw "Destination is a file: $Dest" }

$separator = [IO.Path]::DirectorySeparatorChar
$comparison = if ($separator -eq '\') { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
$sourcePrefix = $Source.TrimEnd($separator) + $separator
$destPrefix = $Dest.TrimEnd($separator) + $separator
if ($sourcePrefix.StartsWith($destPrefix, $comparison) -or $destPrefix.StartsWith($sourcePrefix, $comparison)) {
    throw 'Source and Dest must be different directories and must not contain each other.'
}

$hash = (Invoke-Git @('rev-parse', '--verify', '--end-of-options', "$Commit^{commit}")).Trim()
$parents = (Invoke-Git @('rev-list', '--parents', '-n', '1', $hash)).Trim().Split(' ')
$diffArguments = @('diff-tree', '--root', '--no-commit-id', '--raw', '--no-abbrev', '-r', '-z', '--no-renames')
if ($parents.Count -gt 1) { $diffArguments += $parents[1] }
$diffArguments += @($hash, '--')
$raw = Invoke-Git $diffArguments
$fields = $raw.Split([char]0)
$changes = @()
for ($index = 0; $index -lt $fields.Count - 1; $index += 2) {
    if ($fields[$index] -notmatch '^:([0-9]{6}) ([0-9]{6}) ([0-9a-f]+) ([0-9a-f]+) ([AMDT])$') {
        throw "Unsupported Git change record: $($fields[$index])"
    }
    $oldMode, $newMode, $blob, $status = $Matches[1], $Matches[2], $Matches[4], $Matches[5]
    $path = $fields[$index + 1]
    if ($oldMode -notin @('000000', '100644', '100755') -or $newMode -notin @('000000', '100644', '100755')) {
        throw "Only regular files are supported; unsupported file mode at: $path"
    }
    # Do not let repository paths escape Dest or overwrite Git metadata.
    $parts = $path.Split('/')
    if ($parts | Where-Object { $_ -in @('', '.', '..', '.git') -or $_.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 }) {
        throw "Unsafe or unsupported repository path: $path"
    }
    $target = [IO.Path]::GetFullPath((Join-Path $Dest ($parts -join $separator)))
    if (-not $target.StartsWith($destPrefix, $comparison)) { throw "Path escapes destination: $path" }
    Assert-NoLinks $target
    if (Test-Path -LiteralPath $target -PathType Container) { throw "Destination directory conflicts with file: $path" }
    $parent = Split-Path -Path $target -Parent
    while ($parent) {
        if (Test-Path -LiteralPath $parent -PathType Leaf) { throw "Destination file blocks directory: $parent" }
        $parent = Split-Path -Path $parent -Parent
    }
    $changes += [pscustomobject]@{ Status = $status; Path = $path; Target = $target; Blob = $blob; Staged = $null }
}

Write-Host "Commit: $hash"
foreach ($change in $changes) {
    $action = if ($change.Status -eq 'D') { if ($NoDelete) { 'SKIP DELETE' } else { 'DELETE' } } else { 'COPY' }
    Write-Host "$action $($change.Path)"
}
if ($Preview -or $changes.Count -eq 0) { return }

# Read every blob before changing Dest. Stream binary contents without text conversion.
$staging = Join-Path ([IO.Path]::GetTempPath()) ('copy-git-commit-' + [guid]::NewGuid())
try {
    $null = [IO.Directory]::CreateDirectory($staging)
    foreach ($change in $changes | Where-Object Status -ne 'D') {
        $change.Staged = Join-Path $staging ([guid]::NewGuid().ToString())
        Invoke-Git @('cat-file', 'blob', $change.Blob) $change.Staged
    }
    foreach ($change in $changes) {
        Assert-NoLinks $change.Target
        if ($change.Status -eq 'D') {
            if (-not $NoDelete -and (Test-Path -LiteralPath $change.Target -PathType Leaf)) {
                Remove-Item -LiteralPath $change.Target -Force
            }
        } else {
            $null = [IO.Directory]::CreateDirectory((Split-Path -Path $change.Target -Parent))
            [IO.File]::Copy($change.Staged, $change.Target, $true)
        }
    }
} finally {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
}
