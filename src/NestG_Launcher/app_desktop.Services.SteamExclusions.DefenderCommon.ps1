$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$env:PSModulePath = [IO.Path]::Combine($PSHOME, 'Modules')
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
# Load only the Windows-supplied module, never a module in the working directory/user profile.
Import-Module (Join-Path $PSHOME 'Modules\Defender\Defender.psd1') -ErrorAction Stop

function Test-NestGAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    try { return ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) }
    finally { $identity.Dispose() }
}

function Get-NestGDefenderPolicy {
    $flags = @{}
    foreach ($keyPath in @(
        'SOFTWARE\Policies\Microsoft\Windows Defender',
        'SOFTWARE\Policies\Microsoft\Windows Defender\Exclusions',
        'SOFTWARE\Microsoft\Windows Defender',
        'SOFTWARE\Microsoft\Windows Defender\Exclusions',
        'SOFTWARE\Microsoft\Windows Defender\Features')) {
        $key = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey($keyPath)
        if ($null -ne $key) {
            try {
                foreach ($name in @('HideExclusionsFromLocalAdmins', 'HideExclusionsFromLocalUsers', 'DisableLocalAdminMerge', 'TPExclusions')) {
                    if ($key.GetValue($name, 0) -eq 1) { $flags[$name] = $true }
                }
            } finally { $key.Dispose() }
        }
    }
    return $flags
}

function Get-NestGDefenderSnapshot {
    $admin = Test-NestGAdministrator
    try {
        $status = Defender\Get-MpComputerStatus -ErrorAction Stop
        if (-not $status.AMServiceEnabled -or -not $status.AntivirusEnabled -or
            ($status.AMRunningMode -and $status.AMRunningMode -ne 'Normal')) {
            return @{ Status = 'Unavailable'; Paths = @() }
        }
        $preferences = Defender\Get-MpPreference -ErrorAction Stop
        # Hiding from ordinary users does not mean hiding from an elevated administrator.
        if ($preferences.HideExclusionsFromLocalAdmins) { return @{ Status = 'Blocked'; Paths = @() } }
        if (-not $admin -and $preferences.HideExclusionsFromLocalUsers) {
            return @{ Status = 'NeedsElevation'; Paths = @() }
        }
        $policy = Get-NestGDefenderPolicy
        if ($policy.HideExclusionsFromLocalAdmins -or $policy.DisableLocalAdminMerge -or $policy.TPExclusions) {
            return @{ Status = 'Blocked'; Paths = @() }
        }
        if (-not $admin -and $policy.HideExclusionsFromLocalUsers) { return @{ Status = 'NeedsElevation'; Paths = @() } }
        if ($preferences.DisableLocalAdminMerge) { return @{ Status = 'Blocked'; Paths = @() } }
        if ($null -eq $preferences.PSObject.Properties['ExclusionPath']) {
            return @{ Status = 'Unknown'; Paths = @() }
        }
        $paths = @($preferences.ExclusionPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        # Do not guess the coverage of wildcard/contextual/environment-variable exclusions.
        foreach ($path in $paths) {
            if ($path -notmatch '^[a-zA-Z]:[\\/]' -or $path -match '[*?\[\]%<>|"\x00-\x1f]' -or
                $path.Substring(2).Contains(':')) {
                return @{ Status = 'Unknown'; Paths = @() }
            }
        }
        return @{ Status = 'Ready'; Paths = @($paths) }
    } catch {
        $exception = $_.Exception.GetBaseException()
        if ($exception -is [Security.SecurityException] -or $exception -is [UnauthorizedAccessException]) {
            if (-not $admin) { return @{ Status = 'NeedsElevation'; Paths = @() } }
            return @{ Status = 'Blocked'; Paths = @() }
        }
        return @{ Status = 'Unknown'; Paths = @() }
    }
}

function Test-NestGCovered([string] $path, $exclusions) {
    foreach ($entry in $exclusions) {
        $parent = [IO.Path]::GetFullPath($entry.Replace('/', '\')).TrimEnd('\')
        if ($path.Equals($parent, [StringComparison]::OrdinalIgnoreCase) -or
            $path.StartsWith($parent + '\', [StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

function Test-NestGGameFolder([string] $path) {
    if ($path -notmatch '^[a-zA-Z]:\\' -or $path.Length -gt 1024 -or
        $path -match '[*?\[\]%<>|"\x00-\x1f]' -or $path.Substring(2).Contains(':') -or
        -not $path.EndsWith('\steamapps\common', [StringComparison]::OrdinalIgnoreCase)) { return $false }
    foreach ($part in $path.Split('\')) {
        if ($part -eq '.' -or $part -eq '..' -or $part.EndsWith('.') -or $part.EndsWith(' ')) { return $false }
    }
    $windows = [Environment]::GetFolderPath('Windows').TrimEnd('\')
    if ($path.StartsWith($windows + '\', [StringComparison]::OrdinalIgnoreCase)) { return $false }
    $directory = [IO.DirectoryInfo]::new($path)
    if (-not $directory.Exists) { return $false }
    while ($null -ne $directory) {
        if (($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { return $false }
        $directory = $directory.Parent
    }
    return $true
}
