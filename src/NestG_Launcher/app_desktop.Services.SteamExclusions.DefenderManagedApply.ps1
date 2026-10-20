# $request contains data only. Exit codes are the bounded result protocol; no user-writable result file.
function Test-NestGRequestAlive {
    if ([DateTime]::UtcNow.Ticks -gt [long]$request.DeadlineUtcTicks) { return $false }
    try {
        $parent = [Diagnostics.Process]::GetProcessById([int]$request.ParentId)
        try { return $parent.StartTime.ToUniversalTime().Ticks -eq [long]$request.ParentStartUtcTicks }
        finally { $parent.Dispose() }
    } catch { return $false }
}

function Get-NestGReadFailure($snapshot) {
    switch ($snapshot.Status) {
        'Ready' { return 0 }
        'NeedsElevation' { return 24 }
        'Blocked' { return 22 }
        'Unavailable' { return 23 }
        default { return 26 }
    }
}

function Invoke-NestGManagedApply {
    if (-not (Test-NestGAdministrator)) { return 24 }
    if (-not (Test-NestGRequestAlive)) { return 14 }
    $paths = @($request.Paths | Select-Object -Unique)
    if ($request.Action -notin @('Enable', 'Disable') -or $paths.Count -lt 1 -or $paths.Count -gt 256) { return 14 }
    foreach ($path in $paths) {
        if (-not (Test-NestGOwnedPath $path)) { return 14 }
        if ($request.Action -eq 'Enable' -and -not (Test-NestGGameFolder $path)) { return 14 }
    }
    if ($request.Action -eq 'Enable' -and $paths.Count -gt 32) { return 14 }
    $snapshot = Get-NestGDefenderSnapshot
    $readFailure = Get-NestGReadFailure $snapshot
    if ($readFailure -ne 0) { return $readFailure }
    try { $owned = @(Get-NestGOwnedPaths) } catch { return 25 }
    # Reconcile externally removed exact entries before deciding which paths this operation creates.
    $owned = @($owned | Where-Object { $_ -in $snapshot.Paths })
    try { Set-NestGOwnedPaths $owned } catch { return 25 }
    $removed = @()
    foreach ($path in $paths) {
        if (-not (Test-NestGRequestAlive)) { return 14 }
        # Read immediately before each change, under the shared NestG operation lock.
        $snapshot = Get-NestGDefenderSnapshot
        $readFailure = Get-NestGReadFailure $snapshot
        if ($readFailure -ne 0) { return $readFailure }
        if ($request.Action -eq 'Enable') {
            if (-not (Test-NestGGameFolder $path)) { return 14 }
            if (Test-NestGCovered $path $snapshot.Paths) { continue }
            if ($owned.Count -ge 256) { return 25 }
            Defender\Add-MpPreference -ExclusionPath $path -ErrorAction Stop
            $verified = Get-NestGDefenderSnapshot
            if ($verified.Status -ne 'Ready' -or $path -notin $verified.Paths) { return 13 }
            # Record only a newly added, verified exact entry. Existing parent exclusions are never owned.
            $owned = @($owned + $path | Select-Object -Unique)
            try { Set-NestGOwnedPaths $owned } catch { return 25 }
        } else {
            # The authoritative ledger, not the caller's path list or old consent file, authorizes removal.
            if ($path -notin $owned) { continue }
            if ($path -in $snapshot.Paths) {
                Defender\Remove-MpPreference -ExclusionPath $path -ErrorAction Stop
                $verified = Get-NestGDefenderSnapshot
                if ($verified.Status -ne 'Ready' -or $path -in $verified.Paths) { return 13 }
                $removed += $path
            }
            $owned = @($owned | Where-Object { $_ -ine $path })
            try { Set-NestGOwnedPaths $owned } catch { return 25 }
        }
    }
    $verified = Get-NestGDefenderSnapshot
    if ($verified.Status -ne 'Ready') { return 13 }
    foreach ($path in $removed) { if ($path -in $verified.Paths) { return 13 } }
    foreach ($path in $paths) {
        if ($request.Action -eq 'Enable' -and -not (Test-NestGCovered $path $verified.Paths)) { return 13 }
    }
    if ($request.Action -eq 'Disable') { return 20 }
    return 0
}

$mutex = $null
$acquired = $false
$result = 12
try {
    $mutex = [Threading.Mutex]::new($false, 'Global\NestG.SteamExclusions.v2')
    try { $acquired = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $acquired = $true }
    if ($acquired) { $result = Invoke-NestGManagedApply } else { $result = 27 }
} catch { $result = 12 }
finally {
    if ($acquired) { $mutex.ReleaseMutex() }
    if ($null -ne $mutex) { $mutex.Dispose() }
}
exit $result
