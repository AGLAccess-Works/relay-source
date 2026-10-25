$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$toolsDir = Join-Path $repo '.tools'
$setup = Join-Path $toolsDir 'innosetup-6.7.3.exe'
$destination = Join-Path $toolsDir 'InnoSetup'
New-Item -ItemType Directory -Path $toolsDir -Force | Out-Null
if (!(Test-Path -LiteralPath $setup)) {
    Invoke-WebRequest -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $setup
}
$signature = Get-AuthenticodeSignature -LiteralPath $setup
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notlike '*Pyrsys B.V.*') {
    throw 'The Inno Setup download did not have a valid signature from its official publisher.'
}
$process = Start-Process -FilePath $setup -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/NOICONS','/TASKS=!',"/DIR=`"$destination`"") -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Inno Setup installation returned $($process.ExitCode)." }
if (!(Test-Path -LiteralPath (Join-Path $destination 'ISCC.exe'))) { throw 'Installer compiler was not found after setup.' }
Write-Output "Installer compiler ready: $destination"
