[CmdletBinding()]
param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$PreviousVersion)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version = ([xml](Get-Content (Join-Path $repo 'Directory.Build.props'))).Project.PropertyGroup.Version
$setup = Join-Path $repo "artifacts\release\Relay-$version-win-x64-Setup.exe"
$testRoot = Join-Path $repo 'artifacts\installer-test'
$installDir = Join-Path $testRoot 'app'
$registryPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{20880620-41DE-486D-AC32-F8B581A9A482}_is1'
$groupName = 'Relay'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) "$groupName\Relay.lnk"
$legacyShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Xbox Controller Mouse\Xbox Controller Mouse.lnk'
if (!(Test-Path -LiteralPath $setup)) { throw 'Build the installer first.' }
if (Test-Path -LiteralPath $registryPath) { throw 'The app is already installed. Run this test in a disposable Windows account to avoid changing that installation.' }
$startup = Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name XboxControllerMouse -ErrorAction SilentlyContinue
if ($null -ne $startup) { throw 'An existing startup preference is present. Use a disposable Windows account for this test.' }
if (Test-Path -LiteralPath $installDir) { throw 'The installer test directory already exists. Inspect it before running this test again.' }
if ((Test-Path -LiteralPath $shortcut) -or (Test-Path -LiteralPath $legacyShortcut)) { throw 'An existing Relay or Xbox Controller Mouse shortcut is present. Use a disposable Windows account for this test.' }
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$results = [Collections.Generic.List[string]]::new()
function Invoke-Setup([string]$logName, [string]$package = $setup) {
    $logPath = Join-Path $testRoot $logName
    $process = Start-Process -FilePath $package -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/TASKS=!desktopicon',"/DIR=`"$installDir`"", "/GROUP=`"$groupName`"", "/LOG=`"$logPath`"") -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Installer failed: $($process.ExitCode). See $logPath" }
}
try {
    if ($PreviousVersion) {
        $previousSetup = Join-Path $repo "artifacts\release\XboxControllerMouse-$PreviousVersion-win-x64-Setup.exe"
        if (!(Test-Path -LiteralPath $previousSetup)) { throw 'The requested previous-version installer does not exist.' }
        Invoke-Setup 'install-previous.log' $previousSetup
        $previousExe = Join-Path $installDir 'XboxControllerMouse.exe'
        if ((Get-Item -LiteralPath $previousExe).VersionInfo.ProductVersion -notlike "$PreviousVersion*") { throw 'Previous-version installation did not match the requested version.' }
        Invoke-Setup 'upgrade-previous.log'
        if ((Get-Item -LiteralPath $previousExe).VersionInfo.ProductVersion -notlike "$version*") { throw 'Upgrade did not replace the executable with the current version.' }
        $results.Add("PASS: upgrade from $PreviousVersion to $version, including executable version verification.")
    } else {
        Invoke-Setup 'install.log'
    }
    $appExe = Join-Path $installDir 'XboxControllerMouse.exe'
    if (!(Test-Path -LiteralPath $appExe) -or !(Test-Path -LiteralPath $registryPath)) { throw 'Install did not create the app and uninstall registration.' }
    if (!(Test-Path -LiteralPath $shortcut)) { throw 'Start menu shortcut was not created.' }
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($shortcut)
    if ($link.TargetPath -ne $appExe) { throw 'Start menu shortcut points at the wrong executable.' }
    $results.Add('PASS: per-user install, executable, uninstall registration, and Start menu shortcut target.')
    $smokeDir = Join-Path $testRoot 'installed-smoke'
    $app = Start-Process -FilePath $appExe -ArgumentList @('--smoke-test', "`"$smokeDir`"") -WindowStyle Hidden -Wait -PassThru
    if ($app.ExitCode -ne 0 -or !(Test-Path -LiteralPath (Join-Path $smokeDir 'result.txt'))) { throw 'Installed app smoke test failed.' }
    $results.Add('PASS: installed self-contained app startup, UI, settings, and paused hardware service smoke test.')
    Invoke-Setup 'upgrade.log'
    if (!(Test-Path -LiteralPath $appExe)) { throw 'Same-version reinstall lost the executable.' }
    $results.Add('PASS: same-version upgrade/reinstall.')
} finally {
    $uninstaller = [IO.Path]::GetFullPath((Join-Path $installDir 'unins000.exe'))
    if (!$uninstaller.StartsWith([IO.Path]::GetFullPath($testRoot) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Uninstaller path escaped the test workspace.' }
    if (Test-Path -LiteralPath $uninstaller) {
        $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/LOG=`"$(Join-Path $testRoot 'uninstall.log')`"") -WindowStyle Hidden -Wait -PassThru
        if ($uninstall.ExitCode -ne 0) { throw "Uninstall returned $($uninstall.ExitCode)." }
    }
}
if ((Test-Path -LiteralPath (Join-Path $installDir 'XboxControllerMouse.exe')) -or (Test-Path -LiteralPath $registryPath) -or (Test-Path -LiteralPath $shortcut)) { throw 'Uninstall left application files, registration, or the shortcut behind.' }
$results.Add('PASS: uninstall removed the executable, uninstall registration, and test shortcut.')
$results | Set-Content -LiteralPath (Join-Path $testRoot 'result.txt')
$results
