# $request is decoded from JSON data by the caller, never evaluated as script.
function Test-NestGRequestAlive {
    if ([DateTime]::UtcNow.Ticks -gt [long]$request.DeadlineUtcTicks) { return $false }
    try {
        $parent = [Diagnostics.Process]::GetProcessById([int]$request.ParentId)
        try { return $parent.StartTime.ToUniversalTime().Ticks -eq [long]$request.ParentStartUtcTicks }
        finally { $parent.Dispose() }
    } catch { return $false }
}

try {
    $paths = @($request.Paths)
    if ($paths.Count -lt 1 -or $paths.Count -gt 32 -or -not (Test-NestGRequestAlive)) { exit 14 }
    # Validate the entire batch again after UAC, including reparse points and removed drives.
    foreach ($path in $paths) { if (-not (Test-NestGGameFolder $path)) { exit 14 } }
    $snapshot = Get-NestGDefenderSnapshot
    if ($snapshot.Status -ne 'Ready') { exit 12 }
    foreach ($path in $paths) {
        if (-not (Test-NestGRequestAlive) -or -not (Test-NestGGameFolder $path)) { exit 14 }
        if (-not (Test-NestGCovered $path $snapshot.Paths)) {
            # Additive ONLY. Never replace/remove exclusions or disable protection/policies.
            Defender\Add-MpPreference -ExclusionPath $path -ErrorAction Stop
        }
    }
    $verified = Get-NestGDefenderSnapshot
    if ($verified.Status -ne 'Ready') { exit 13 }
    foreach ($path in $paths) { if (-not (Test-NestGCovered $path $verified.Paths)) { exit 13 } }
    exit 0
} catch { exit 12 }
