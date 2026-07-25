# Pulls the SpotiSharp radio diagnostics log off a USB-connected phone.
#
# Usage:  .\tools\pull-radio-log.ps1 [-Tail 40]
#
# Requires: USB debugging enabled on the phone, and a debuggable (debug) build of the
# app installed -- adb's run-as only works for debuggable packages. The log is written
# during normal use by every build; this script is just how you get it off the phone.

param([int]$Tail = 40)

$package = "com.companyname.spotisharp"

$adb = (Get-Command adb -ErrorAction SilentlyContinue).Source
if (-not $adb) {
    foreach ($candidate in @(
            "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe",
            "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe")) {
        if (Test-Path $candidate) { $adb = $candidate; break }
    }
}
if (-not $adb) {
    Write-Error "adb not found. Install Android platform-tools or add adb to PATH."
    exit 1
}

$connected = & $adb devices | Select-Object -Skip 1 | Where-Object { $_ -match "`tdevice$" }
if (-not $connected) {
    Write-Error "No device connected. Plug the phone in and check USB debugging is enabled."
    exit 1
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$out = "radio-diagnostics-$stamp.log"

# older rotated generation first (if any), then the current file
& $adb exec-out run-as $package sh -c "cat files/radio-diagnostics.old.log 2>/dev/null; cat files/radio-diagnostics.log 2>/dev/null" | Set-Content -Encoding utf8 $out

if (-not (Test-Path $out) -or (Get-Item $out).Length -eq 0) {
    Write-Error "No log retrieved. Is a debug build of the app installed, and has it been launched at least once?"
    exit 1
}

Write-Host "Saved to $out`n--- last $Tail lines ---"
Get-Content $out -Tail $Tail
