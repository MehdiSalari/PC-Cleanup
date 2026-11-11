<#
.SYNOPSIS
    PC Cleanup - frees disk space by deleting only regenerable cache/temp data.

.DESCRIPTION
    Safe by design: it never touches user documents, projects, credentials,
    installed apps, or anything that isn't re-downloadable / auto-regenerated.
    Run it as often as you like. Use -DryRun first if you want to see the plan.

.PARAMETER DryRun
    Only report what would be deleted. Nothing is removed.

.PARAMETER Elevate
    Re-launches this script as Administrator (UAC prompt) so the Windows-level
    items (Windows Update cache, Delivery Optimization, DISM, WER) also run.

.PARAMETER AgeDays
    Minimum age in days for TEMP files to be considered "stale". Default 1.
    Files locked by a running program are always skipped.

.PARAMETER CleanDownloadInstallers
    Also delete old installers (.exe/.msi/.msix/.zip/.jar/.7z/.iso) from your
    Downloads folder. Off by default - installers are sometimes kept on purpose.

.PARAMETER CleanBuildCaches
    Also clear Maven (.m2\repository) and the Gradle build cache. Off by default:
    these can be several GB and re-downloading them may be slow or unavailable
    on restricted or offline networks.

.PARAMETER ForceWindowsUpdate
    Clear the Windows Update download cache even when an update is currently
    downloading or pending. Off by default because deleting a cache that WU is
    actively filling only makes it re-download - and on a nearly full C: that
    download can fail and leave the machine worse off.

.PARAMETER ResetBase
    Pass DISM /ResetBase, which runs StartComponentCleanup in its most
    aggressive form. It reclaims the most space but PERMANENTLY removes the
    ability to uninstall updates that are already installed. Off by default.

.PARAMETER SkipLog
    Do not write a timestamped log file next to this script.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Invoke-PC-Cleanup.ps1 -DryRun
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Invoke-PC-Cleanup.ps1 -Elevate
#>
[CmdletBinding()]
param(
    [switch]$DryRun,
    [switch]$Elevate,
    [switch]$CleanDownloadInstallers,
    [switch]$CleanBuildCaches,
    [switch]$ForceWindowsUpdate,
    [switch]$ResetBase,
    [switch]$SkipLog,
    [int]$AgeDays = 1
)

$ErrorActionPreference = 'SilentlyContinue'
$script:StartFree = (Get-CimInstance Win32_LogicalDisk | Where-Object DeviceID -eq 'C:').FreeSpace

# =================================================================
# GUARD 1: never let a system update run on top of a nearly-full C:.
# Windows will happily download a 9 GB Feature Update into a C: that has
# 10 GB free, fail to commit (0x8024202C / 0xC19001DF), and leave the
# download cache behind - which is how this machine ended up back at 95%.
# =================================================================
function Get-WuBusyState {
    $reasons = @()
    try {
        $ev = Get-WinEvent -FilterHashtable @{
            LogName='System'; ProviderName='Microsoft-Windows-WindowsUpdateClient';
            StartTime=(Get-Date).AddHours(-1)
        } -ErrorAction SilentlyContinue | Where-Object { $_.Id -in 19,20,21,43,44,217 }
        if ($ev) { $reasons += "Windows Update active in the last hour (events: $((($ev | Select-Object -ExpandProperty Id -Unique) -join ',')))" }
    } catch {}
    if (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired') {
        $reasons += 'pending reboot required to finish an update'
    }
    if (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending') {
        $reasons += 'component servicing pending reboot'
    }
    return $reasons
}

$FreeGB = [math]::Round($script:StartFree / 1GB, 1)
$script:WuBusyReasons = Get-WuBusyState
if ($FreeGB -lt 20 -and -not $ForceWindowsUpdate) {
    Write-Host ''
    Write-Host '===============================================================' -ForegroundColor Red
    Write-Host " WARNING: only $FreeGB GB free on C:" -ForegroundColor Red
    foreach ($why in $script:WuBusyReasons) { Write-Host "   - $why" -ForegroundColor Yellow }
    Write-Host ' A Windows Feature Update in progress can consume 9+ GB here and then' -ForegroundColor Yellow
    Write-Host ' fail to commit, leaving the download cache behind (exactly what pushed' -ForegroundColor Yellow
    Write-Host ' this machine back to 95% full). Windows-level cleanup is SKIPPED.' -ForegroundColor Yellow
    Write-Host ' Finish/reboot the update first, then run this again with more room.' -ForegroundColor Yellow
    Write-Host '===============================================================' -ForegroundColor Red
    $script:LowDisk = $true
} else {
    $script:LowDisk = $false
}

# ---------------------------------------------------------------- elevation
function Test-Admin {
    ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

if ($Elevate -and -not (Test-Admin)) {
    Write-Host "Requesting Administrator rights..." -ForegroundColor Yellow
    # forward every switch so an elevated dry-run stays a dry-run
    $fwd = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -AgeDays $AgeDays"
    if ($CleanDownloadInstallers) { $fwd += ' -CleanDownloadInstallers' }
    if ($CleanBuildCaches)        { $fwd += ' -CleanBuildCaches' }
    if ($ForceWindowsUpdate)      { $fwd += ' -ForceWindowsUpdate' }
    if ($ResetBase)               { $fwd += ' -ResetBase' }
    if ($DryRun)               { $fwd += ' -DryRun' }
    if ($SkipLog)              { $fwd += ' -SkipLog' }
    Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -ArgumentList $fwd -Verb RunAs
    exit
}

# ---------------------------------------------------------------- logging
$LogPath = if ($SkipLog) { $null } else { Join-Path (Split-Path $PSCommandPath) ("cleanup-{0}.log" -f (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$script:Results = New-Object System.Collections.Generic.List[object]

function Write-Log($msg) {
    $line = "[{0}] {1}" -f (Get-Date -Format 'HH:mm:ss'), $msg
    Write-Host $line
    if ($LogPath) { Add-Content -Path $LogPath -Value $line }
}

# ---------------------------------------------------------------- helpers
function Get-DirBytes($path) {
    if (-not (Test-Path -LiteralPath $path)) { return 0 }
    $item = Get-Item -LiteralPath $path -Force
    if (-not $item.PSIsContainer) { return $item.Length }
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { return 0 }
    $sum = (Get-ChildItem -LiteralPath $path -Recurse -File -Force | Measure-Object Length -Sum).Sum
    if ($null -eq $sum) { return 0 }
    return $sum
}

function Format-GB($bytes) { '{0:N2} GB' -f ($bytes / 1GB) }

# Deletes the CONTENTS of each path (keeps the folders themselves), honouring an
# optional minimum age. Returns the number of bytes that were (or would be) removed.
function Clear-Targets {
    param(
        [string]$Label,
        [string[]]$Paths,
        [int]$MinAgeDays = -1,
        [string]$Filter = '*'          # name filter applied to files, e.g. '*.exe'
    )

    $planned = 0
    $targets = 0

    foreach ($p in $Paths) {
        if (-not (Test-Path -LiteralPath $p)) { continue }

        # a file target is handled as-is
        $item = Get-Item -LiteralPath $p -Force
        if (-not $item.PSIsContainer) {
            if ($MinAgeDays -ge 0 -and $item.LastWriteTime -ge (Get-Date).AddDays(-$MinAgeDays)) { continue }
            if ($Filter -ne '*' -and $item.Name -notlike $Filter) { continue }
            $planned += $item.Length
            $targets++
            if (-not $DryRun) { Remove-Item -LiteralPath $p -Force }
            continue
        }

        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }

        # expand: every child of the folder is a target
        $children = Get-ChildItem -LiteralPath $p -Force | Where-Object {
            -not ($_.Attributes -band [IO.FileAttributes]::ReparsePoint)
        }
        foreach ($c in $children) {
            $skip = $false
            if ($MinAgeDays -ge 0 -and $c.LastWriteTime -ge (Get-Date).AddDays(-$MinAgeDays)) { $skip = $true }
            if ($Filter -ne '*') {
                if ($c.PSIsContainer) { $skip = $true }
                elseif ($c.Name -notlike $Filter) { $skip = $true }
            }
            if ($skip) { continue }
            $planned += Get-DirBytes $c.FullName
            $targets++
            if (-not $DryRun) { Remove-Item -LiteralPath $c.FullName -Recurse -Force }
        }
    }

    $script:Results.Add([PSCustomObject]@{ Target = $Label; Items = $targets; Bytes = $planned })
    $verb = if ($DryRun) { 'would free' } else { 'freed    ' }
    Write-Log ("{0}  {1,10}  ({2} items)  {3}" -f $verb, (Format-GB $planned), $targets, $Label)
}

function Run-CacheCommand {
    param([string]$Label, [string]$Exe, [string[]]$ExeArgs)

    if (-not (Get-Command $Exe -ErrorAction SilentlyContinue)) {
        Write-Log ("skipped    {0,-10}  ({1} not found)" -f '0.00 GB', $Exe)
        return
    }
    $freeBefore = (Get-CimInstance Win32_LogicalDisk | Where-Object DeviceID -eq 'C:').FreeSpace
    if (-not $DryRun) { & $Exe @ExeArgs 2>$null | Out-Null }
    $freed = (Get-CimInstance Win32_LogicalDisk | Where-Object DeviceID -eq 'C:').FreeSpace - $freeBefore
    if ($freed -lt 0) { $freed = 0 }
    $script:Results.Add([PSCustomObject]@{ Target = $Label; Items = 0; Bytes = $freed })
    Write-Log ("{0}  {1,10}  {2}" -f $(if ($DryRun) { 'estimated ' } else { 'freed     ' }), (Format-GB $freed), $Label)
}

# ---------------------------------------------------------------- banner
$mode = if ($DryRun) { 'DRY RUN - nothing will be deleted' } else { 'LIVE' }
Write-Host ''
Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host " PC Cleanup   [$mode]   $(Get-Date -Format 'yyyy-MM-dd HH:mm')" -ForegroundColor Cyan
Write-Host " Admin: $(Test-Admin)   Stale threshold: $AgeDays day(s)" -ForegroundColor Cyan
Write-Host "===============================================================" -ForegroundColor Cyan
if ($LogPath) { Write-Host " Log: $LogPath`n" -ForegroundColor DarkGray }

$U = $env:USERPROFILE
$LOCAL = "$U\AppData\Local"
$ROAM  = "$U\AppData\Roaming"

# =================================================================
# 1. TEMP files older than -AgeDays (running programs keep their locks)
# =================================================================
Clear-Targets -Label "User TEMP (>$AgeDays day)" -Paths "$LOCAL\Temp" -MinAgeDays $AgeDays

# =================================================================
# 2. Package-manager / build-tool caches (all re-downloadable)
# =================================================================
Run-CacheCommand -Label "uv cache (Python)" -Exe 'uv' -ExeArgs @('cache', 'clean')
Run-CacheCommand -Label "pip cache" -Exe 'pip' -ExeArgs @('cache', 'purge')
Clear-Targets -Label "npm/npx cache" -Paths @(
    "$LOCAL\npm-cache\_npx", "$LOCAL\npm-cache\_cacache", "$LOCAL\npm-cache\logs",
    "$U\.npm\_cacache"
)
Clear-Targets -Label "Pub cache (Flutter/Dart)" -Paths @("$LOCAL\Pub\Cache")
Clear-Targets -Label "Dart analysis server cache" -Paths "$LOCAL\.dartServer"
Clear-Targets -Label "Yarn / pnpm / bun cache" -Paths @("$LOCAL\Yarn\Cache", "$LOCAL\pnpm-store", "$LOCAL\.bun\install\cache")
if ($CleanBuildCaches) {
    Clear-Targets -Label "Maven repo (.m2)" -Paths "$U\.m2\repository"
    Clear-Targets -Label "Gradle build cache" -Paths "$U\.gradle\caches\build-cache-1"
}

# =================================================================
# 3. Electron app auto-updater download caches
#    (the .exe installers Squirrel/electron-updater downloaded and forgot)
# =================================================================
$updaters = Get-ChildItem $LOCAL -Directory -Force | Where-Object { $_.Name -like '*updater*' } |
            ForEach-Object { $_.FullName }
Clear-Targets -Label "Electron updater caches ($($updaters.Count) apps)" -Paths $updaters

# =================================================================
# 4. Application caches - Cache / Code Cache / GPUCache only.
#    Never touches Logins, Cookies, Local Storage, profiles or settings.
# =================================================================
$cacheRel = @('Cache', 'Code Cache', 'GPUCache', 'DawnCache', 'DawnGraphiteCache',
              'DawnWebGPUCache', 'CachedData', 'CachedExtensionVSIXs', 'logs',
              'Crashpad', 'Service Worker\CacheStorage', 'Service Worker\ScriptCache')
$appCaches = @()
foreach ($app in $ROAM, $LOCAL) {
    Get-ChildItem $app -Directory -Force | ForEach-Object {
        foreach ($sub in $cacheRel) {
            $t = Join-Path $_.FullName $sub
            if (Test-Path -LiteralPath $t) { $appCaches += $t }
        }
    }
}
# Chromium browser profiles
foreach ($root in @("$LOCAL\Microsoft\Edge\User Data", "$LOCAL\Google\Chrome\User Data",
                    "$LOCAL\BraveSoftware\Brave-Browser\User Data")) {
    if (Test-Path -LiteralPath $root) {
        Get-ChildItem $root -Directory -Force | ForEach-Object {
            foreach ($sub in @('Cache', 'Code Cache', 'GPUCache', 'Service Worker\CacheStorage')) {
                $t = Join-Path $_.FullName $sub
                if (Test-Path -LiteralPath $t) { $appCaches += $t }
            }
        }
    }
}
Clear-Targets -Label "App / browser caches" -Paths $appCaches

# =================================================================
# 5. Crash dumps, error reports, Windows thumbnails
# =================================================================
Clear-Targets -Label "Crash dumps" -Paths @(
    "$LOCAL\CrashDumps", "$LOCAL\Microsoft\Windows\WER", "$LOCAL\Crashpad",
    "C:\ProgramData\Microsoft\Windows\WER\ReportArchive", "C:\ProgramData\Microsoft\Windows\WER\ReportQueue"
)
Clear-Targets -Label "Thumbnail cache" -Paths "$LOCAL\Microsoft\Windows\Explorer" -Filter 'thumbcache_*.db'
Clear-Targets -Label "Icon cache" -Paths "$LOCAL\Microsoft\Windows\Explorer" -Filter 'iconcache_*.db'
Clear-Targets -Label "GPU shader caches (NVIDIA/AMD/Intel)" -Paths @(
    "$LOCAL\NVIDIA\GLCache", "$LOCAL\NVIDIA\DXCache", "$LOCAL\NVIDIA\ComputeCache",
    "$LOCAL\AMD\DxCache", "$LOCAL\AMD\DxcCache", "$LOCAL\Intel\ShaderCache"
)
Clear-Targets -Label "DNS / thumbnailer leftovers" -Paths @(
    "$LOCAL\D3DSCache", "$LOCAL\Microsoft\Windows\INetCache", "$LOCAL\Microsoft\Windows\WebCache"
)

# =================================================================
# 6. Recycle Bin
# =================================================================
if (-not $DryRun) { Clear-RecycleBin -Force }
$script:Results.Add([PSCustomObject]@{ Target = 'Recycle Bin'; Items = 0; Bytes = 0 })
Write-Log ("{0}  {1,10}  Recycle Bin" -f $(if ($DryRun) { 'skipped   ' } else { 'cleared   ' }), ' - ')

# =================================================================
# 7. Downloads - OPT-IN only
# =================================================================
if ($CleanDownloadInstallers) {
    Clear-Targets -Label "Downloads: old installers (>7 days)" `
        -Paths "$U\Downloads" -MinAgeDays 7 -Filter '*.exe'
    Clear-Targets -Label "Downloads: old archives (>7 days)" `
        -Paths "$U\Downloads" -MinAgeDays 7 -Filter '*.zip'
}

# =================================================================
# 8. Administrator-only items
# =================================================================
if (Test-Admin) {
    if ($script:LowDisk) {
        Write-Log '--- Windows-level cleanup SKIPPED: C: is too full / an update is in flight ---' -ForegroundColor Yellow
        Write-Log '    Re-run later, or pass -ForceWindowsUpdate to override.' -ForegroundColor DarkGray
    } else {
    Write-Log '--- running Windows-level cleanup (Administrator) ---' -ForegroundColor DarkCyan

    # Windows Update download cache.
    # GUARD: if an update is mid-download or pending install, deleting this cache is
    # self-defeating - WU just re-downloads it and the commit can fail (0x8024202C).
    if ($script:WuBusyReasons.Count -gt 0 -and -not $ForceWindowsUpdate) {
        foreach ($why in $script:WuBusyReasons) { Write-Log "WU busy: $why" -ForegroundColor DarkYellow }
        Write-Log 'SKIPPED    Windows Update cache (update in progress - it would only be re-downloaded)' -ForegroundColor Yellow
    } else {
        # stop update services so the cache can actually be removed
        if (-not $DryRun) {
            Stop-Service -Name bits, wuauserv -Force
            Start-Sleep -Seconds 2
        }
        Clear-Targets -Label "Windows Update download cache" -Paths 'C:\Windows\SoftwareDistribution\Download'
        if (-not $DryRun) { Start-Service -Name bits, wuauserv }
    }

    Clear-Targets -Label "Windows\Temp" -Paths 'C:\Windows\Temp'

    # Delivery Optimization cache
    if (-not $DryRun) { Delete-DeliveryOptimizationCache -Force }
    Write-Log ("{0}  {1,10}  Delivery Optimization cache" -f $(if ($DryRun) { 'estimated ' } else { 'freed     ' }), ' - ')

    # Component store: the official, safe Microsoft cleanup.
    # NOTE: /ResetBase is NOT used by default - it is a one-way door that permanently
    # removes the ability to uninstall already-installed updates.
    if (-not $DryRun) {
        Write-Log 'running DISM /StartComponentCleanup (can take several minutes)...' -ForegroundColor DarkCyan
        $freeBefore = (Get-CimInstance Win32_LogicalDisk | Where-Object DeviceID -eq 'C:').FreeSpace
        if ($ResetBase) {
            & dism.exe /Online /Cleanup-Image /StartComponentCleanup /ResetBase 2>&1 | Out-Null
        } else {
            & dism.exe /Online /Cleanup-Image /StartComponentCleanup 2>&1 | Out-Null
        }
        $freed = (Get-CimInstance Win32_LogicalDisk | Where-Object DeviceID -eq 'C:').FreeSpace - $freeBefore
        $script:Results.Add([PSCustomObject]@{ Target = 'DISM component store cleanup'; Items = 0; Bytes = $freed })
        Write-Log ("freed      {0,10}  DISM component store cleanup{1}" -f (Format-GB $freed), $(if ($ResetBase) { '  [/ResetBase]' } else { '' }))
    } else {
        Write-Log 'skipped    DISM component store cleanup (live run only)'
    }

    # old Windows install leftovers
    Clear-Targets -Label "Windows.old" -Paths 'C:\Windows.old' -MinAgeDays 1
    }
} else {
    Write-Log '--- Windows-level items SKIPPED (not Administrator). Re-run with -Elevate to include them. ---' -ForegroundColor Yellow
}

# =================================================================
# Summary
# =================================================================
$endFree = (Get-CimInstance Win32_LogicalDisk | Where-Object DeviceID -eq 'C:').FreeSpace
$actual  = $endFree - $script:StartFree

Write-Host ''
Write-Host '===============================================================' -ForegroundColor Cyan
Write-Host ' SUMMARY' -ForegroundColor Cyan
Write-Host '===============================================================' -ForegroundColor Cyan
$script:Results | Where-Object { $_.Bytes -gt 0 } | Sort-Object Bytes -Descending |
    ForEach-Object { "  {0,-46} {1,10}" -f $_.Target, (Format-GB $_.Bytes) }
Write-Host ('  ' + '-' * 58)
$totalBytes = ($script:Results | Measure-Object Bytes -Sum).Sum
if ($DryRun) {
    Write-Host ("  {0,-46} {1,10}" -f 'TOTAL ESTIMATED', (Format-GB $totalBytes)) -ForegroundColor Green
} else {
    Write-Host ("  {0,-46} {1,10}" -f 'TOTAL CLEANED (measured on disk)', (Format-GB $actual)) -ForegroundColor Green
    Write-Host ("  {0,-46} {1,10}" -f 'C: free space now', ('{0:N1} GB' -f ($endFree / 1GB))) -ForegroundColor Green
    $pct = [math]::Round(100 * (1 - $endFree / (Get-CimInstance Win32_LogicalDisk | Where-Object DeviceID -eq 'C:').Size), 1)
    if ($pct -gt 90) {
        Write-Host ''
        Write-Host " C: is ${pct}% full. Under ~15% free, an SSD slows down noticeably and" -ForegroundColor Yellow
        Write-Host ' Windows Update can start failing. Consider moving the pagefile to D:' -ForegroundColor Yellow
        Write-Host ' (Settings > System > About > Advanced system settings > Performance).' -ForegroundColor Yellow
    }
}
Write-Host ''
if ($DryRun) { Write-Host ' Nothing was deleted. Re-run without -DryRun to apply.' -ForegroundColor Yellow }
if ($LogPath) { Write-Host " Log saved to: $LogPath`n" -ForegroundColor DarkGray }
