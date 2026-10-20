# Machine-wide ledger. Only administrators/SYSTEM can create or change ownership evidence.
$script:NestGLedgerPath = 'SOFTWARE\NestGLauncher.SteamExclusions.v2'

function Test-NestGOwnedPath([string] $path) {
    if ($path -notmatch '^[a-zA-Z]:\\' -or $path.Length -gt 1024 -or
        $path -match '[*?\[\]%<>|"\x00-\x1f]' -or $path.Substring(2).Contains(':') -or
        -not $path.EndsWith('\steamapps\common', [StringComparison]::OrdinalIgnoreCase)) { return $false }
    foreach ($part in $path.Split('\')) {
        if ($part -eq '.' -or $part -eq '..' -or $part.EndsWith('.') -or $part.EndsWith(' ')) { return $false }
    }
    $windows = [Environment]::GetFolderPath('Windows').TrimEnd('\')
    return -not $path.StartsWith($windows + '\', [StringComparison]::OrdinalIgnoreCase)
}

function Assert-NestGLedgerSecurity($key) {
    $acl = $key.GetAccessControl()
    $trusted = @('S-1-5-18', 'S-1-5-32-544')
    if ($acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -notin $trusted) { throw 'Untrusted ledger owner.' }
    $write = [Security.AccessControl.RegistryRights]::SetValue -bor [Security.AccessControl.RegistryRights]::CreateSubKey -bor
        [Security.AccessControl.RegistryRights]::ChangePermissions -bor [Security.AccessControl.RegistryRights]::TakeOwnership -bor
        [Security.AccessControl.RegistryRights]::Delete
    foreach ($rule in $acl.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier])) {
        if ($rule.AccessControlType -eq [Security.AccessControl.AccessControlType]::Allow -and
            ($rule.RegistryRights -band $write) -ne 0 -and $rule.IdentityReference.Value -notin $trusted) {
            throw 'Untrusted ledger permissions.'
        }
    }
}

function Open-NestGLedger {
    $key = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey($script:NestGLedgerPath, $true)
    if ($null -eq $key) {
        $acl = [Security.AccessControl.RegistrySecurity]::new()
        $acl.SetAccessRuleProtection($true, $false)
        $acl.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))
        foreach ($sid in @('S-1-5-18', 'S-1-5-32-544')) {
            $acl.AddAccessRule([Security.AccessControl.RegistryAccessRule]::new(
                [Security.Principal.SecurityIdentifier]::new($sid), [Security.AccessControl.RegistryRights]::FullControl,
                [Security.AccessControl.AccessControlType]::Allow))
        }
        $acl.AddAccessRule([Security.AccessControl.RegistryAccessRule]::new(
            [Security.Principal.SecurityIdentifier]::new('S-1-5-11'), [Security.AccessControl.RegistryRights]::ReadKey,
            [Security.AccessControl.AccessControlType]::Allow))
        $key = [Microsoft.Win32.Registry]::LocalMachine.CreateSubKey($script:NestGLedgerPath,
            [Microsoft.Win32.RegistryKeyPermissionCheck]::ReadWriteSubTree, $acl)
    }
    try { Assert-NestGLedgerSecurity $key } catch { $key.Dispose(); throw }
    return $key
}

function Get-NestGOwnedPaths {
    $key = Open-NestGLedger
    try {
        $paths = @($key.GetValue('Paths', [string[]]@()))
        if ($paths.Count -gt 256) { throw 'Invalid ledger size.' }
        foreach ($path in $paths) { if (-not (Test-NestGOwnedPath $path)) { throw 'Invalid ledger path.' } }
        return $paths
    } finally { $key.Dispose() }
}

function Set-NestGOwnedPaths($paths) {
    $paths = @($paths | Select-Object -Unique)
    if ($paths.Count -gt 256) { throw 'Invalid ledger size.' }
    foreach ($path in $paths) { if (-not (Test-NestGOwnedPath $path)) { throw 'Invalid ledger path.' } }
    $key = Open-NestGLedger
    try { $key.SetValue('Paths', [string[]]$paths, [Microsoft.Win32.RegistryValueKind]::MultiString); $key.Flush() }
    finally { $key.Dispose() }
}
