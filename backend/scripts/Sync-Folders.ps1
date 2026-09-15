<#
.SYNOPSIS
Synchronizes files from Source to Dest, deleting destination-only entries.
.DESCRIPTION
Relative paths are resolved against the caller's current PowerShell directory.
Paths containing symbolic links or junctions (including ancestors) are rejected.
Use -Preview to list changes without applying them. Requires Windows Robocopy.
#>
param(
    [Parameter(Mandatory)]
    [string]$Source,

    [Parameter(Mandatory)]
    [string]$Dest,

    [switch]$Preview
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Source -PathType Container)) {
    throw "Source directory not found: $Source"
}

$Source = (Get-Item -LiteralPath $Source).FullName
$Dest = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Dest)

if (Test-Path -LiteralPath $Dest -PathType Leaf) {
    throw "Destination path points to a file: $Dest"
}

# Aliases can hide overlapping trees from the string comparison below.
# Inspect existing ancestors too, including when Dest does not exist yet.
foreach ($path in @($Source, $Dest)) {
    $currentPath = $path
    while ($currentPath) {
        if (Test-Path -LiteralPath $currentPath) {
            $item = Get-Item -LiteralPath $currentPath -Force
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Paths must not contain symbolic links or junctions: $currentPath"
            }
        }
        $currentPath = Split-Path -Path $currentPath -Parent
    }
}

# Reject identical directories and directories nested inside each other.
$separator = [IO.Path]::DirectorySeparatorChar
$sourcePrefix = $Source.TrimEnd($separator) + $separator
$destPrefix = $Dest.TrimEnd($separator) + $separator
$comparison = [StringComparison]::OrdinalIgnoreCase

if ($sourcePrefix.StartsWith($destPrefix, $comparison) -or
    $destPrefix.StartsWith($sourcePrefix, $comparison)) {
    throw "Source and Dest must be different directories and must not contain each other."
}

$options = @(
    '/E'         # Include all subdirectories, including empty ones
    '/PURGE'     # Delete destination entries missing from source
    '/XO'        # Skip source files older than destination files
    '/XC'        # Skip files with equal timestamps but different sizes
    '/COPY:DAT'  # Copy data, attributes, and timestamps
    '/XJ'        # Exclude junction points
    '/R:2'       # Retry failed copies twice
    '/W:1'       # Wait one second between retries
)

if ($Preview) {
    $options += '/L' # List planned changes without modifying files
}

# Handle Robocopy's nonzero success codes ourselves, regardless of caller preferences.
$PSNativeCommandUseErrorActionPreference = $false
& robocopy.exe $Source $Dest @options
$result = $LASTEXITCODE

# Robocopy exit codes from 0 through 7 do not indicate copy failures.
if ($result -ge 8) {
    throw "Robocopy failed with exit code: $result"
}
