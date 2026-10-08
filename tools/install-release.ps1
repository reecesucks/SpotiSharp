# Builds a Release APK and installs it over the app already on a USB-connected phone,
# keeping the app's data (radio settings, "not interested" list, caches, login).
#
# Usage:  .\tools\install-release.ps1 [-SkipBuild]
#
# Always installs with `adb install -r` (replace), never uninstalls. If the installed app
# was signed with a different key, the install fails instead of wiping data, and nothing
# on the phone is touched.

param([switch]$SkipBuild)

$package = "com.companyname.spotisharp"
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo "SpotiSharp\SpotiSharp.csproj"
$apk = Join-Path $repo "SpotiSharp\bin\Release\net9.0-android\publish\$package-Signed.apk"

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

if (-not $SkipBuild) {
    Write-Host "Building Release APK..."
    & dotnet publish $project -f net9.0-android -c Release -v q -nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed."
        exit 1
    }
}

if (-not (Test-Path $apk)) {
    Write-Error "APK not found at $apk"
    exit 1
}

Write-Host "Installing $(Split-Path -Leaf $apk) (keeping app data)..."
$output = & $adb install -r $apk 2>&1 | Out-String
Write-Host $output.Trim()

if ($output -match "INSTALL_FAILED_UPDATE_INCOMPATIBLE") {
    Write-Error ("The installed app is signed with a different key, so it can't be replaced in place. " +
        "Nothing was changed. Uninstalling would wipe the radio settings, so that's left to you.")
    exit 1
}
if ($output -notmatch "Success") {
    Write-Error "Install failed. Nothing was uninstalled."
    exit 1
}

$installed = & $adb shell dumpsys package $package | Select-String "firstInstallTime|lastUpdateTime"
Write-Host ($installed -join "`n")
