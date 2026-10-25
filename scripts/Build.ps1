[CmdletBinding()]
param([switch]$Installer, [switch]$BootstrapInstallerTools,
    [ValidatePattern('^10\.0\.\d+$')][string]$RuntimeFrameworkVersion = '10.0.13')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $repo
try {
    $version = ([xml](Get-Content 'Directory.Build.props')).Project.PropertyGroup.Version
    $fileVersion = ($version -split '-')[0] + '.0'
    $publish = Join-Path $repo "artifacts\publish\$version\win-x64"
    $release = Join-Path $repo 'artifacts\release'
    New-Item -ItemType Directory -Path $release -Force | Out-Null
    if (!(Test-Path 'src\ControllerMouse.App\Assets\app.ico')) { & "$PSScriptRoot\New-Icon.ps1" }
    & dotnet run --project tests\ControllerMouse.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed.' }
    & dotnet publish src\ControllerMouse.App\ControllerMouse.App.csproj -c Release -r win-x64 --self-contained true "-p:RelayRuntimeFrameworkVersion=$RuntimeFrameworkVersion" -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'Publishing failed.' }
    Copy-Item -LiteralPath 'docs\QUICKSTART.md' -Destination $publish
    Copy-Item -LiteralPath 'docs\BETA.md' -Destination $publish
    Copy-Item -LiteralPath 'docs\BETA-VERIFICATION.md' -Destination $publish
    Copy-Item -LiteralPath 'docs\NAVIGATION-FIXES.md' -Destination $publish
    Copy-Item -LiteralPath 'docs\TESTING.md' -Destination $publish
    Copy-Item -LiteralPath 'docs\POLISH-AND-ACCESSIBILITY.md' -Destination $publish
    Copy-Item -LiteralPath 'docs\ACCESSIBILITY.md' -Destination $publish
    Copy-Item -LiteralPath 'docs\VISUAL-ACCESS.md' -Destination $publish
    Copy-Item -LiteralPath 'docs\NARRATOR.md' -Destination $publish
    Copy-Item -LiteralPath 'docs\ADAPTIVE-LEARNING.md' -Destination $publish
    Copy-Item -LiteralPath 'docs\BUTTON-COMBINATIONS.md' -Destination $publish
    Copy-Item -LiteralPath 'docs\GUIDED-SETUP.md' -Destination $publish
    Copy-Item -LiteralPath 'docs\ACCESSIBILITY-CORE-TESTS.md' -Destination $publish
    Copy-Item -LiteralPath 'docs\STABILITY-CORE.md' -Destination $publish
    Copy-Item -LiteralPath 'docs\CONTROLLER-IDENTITY.md' -Destination $publish
    $zip = Join-Path $release "Relay-$version-win-x64-Portable.zip"
    Compress-Archive -Path "$publish\*" -DestinationPath $zip -Force
    if ($Installer) {
        $iscc = @(
            (Join-Path $repo '.tools\InnoSetup\ISCC.exe'),
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
        ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if (!$iscc -and $BootstrapInstallerTools) {
            & "$PSScriptRoot\Get-InstallerTools.ps1"
            $iscc = Join-Path $repo '.tools\InnoSetup\ISCC.exe'
        }
        if (!$iscc) { throw 'Inno Setup 6 is required. Install it, or use -BootstrapInstallerTools to download a verified build tool into .tools.' }
        & $iscc "/DAppVersion=$version" "/DAppFileVersion=$fileVersion" "/DPublishDir=$publish" "/DOutputDir=$release" installer\ControllerMouse.iss
        if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    }
    Get-ChildItem -LiteralPath $release -File | Where-Object Extension -In '.zip','.exe' | ForEach-Object {
        $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
        "$($hash.Hash.ToLowerInvariant())  $($_.Name)"
    } | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt')
    Write-Output "Release files: $release"
} finally { Pop-Location }
