[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [string]$EvidenceDirectory,
    [ValidatePattern('^\d{4}-\d{2}-\d{2}$')][string]$Date = (Get-Date -Format 'yyyy-MM-dd')
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo 'artifacts\source-handoff' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
$version = ([xml](Get-Content -LiteralPath (Join-Path $repo 'Directory.Build.props'))).Project.PropertyGroup.Version
$name = "Relay-Source-$version-$Date"
$package = Join-Path $output $name
$source = Join-Path $package 'Relay'
$zip = Join-Path $output "$name.zip"
if ((Test-Path -LiteralPath $package) -or (Test-Path -LiteralPath $zip)) {
    throw 'This handoff already exists. Choose a new output directory to preserve the previous package.'
}
New-Item -ItemType Directory -Path $source -Force | Out-Null

function Copy-SourceFile([string]$path, [string]$relative) {
    $item = Get-Item -LiteralPath $path
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked source file rejected: $relative" }
    if ($item.Length -ge 95MB) { throw "Source file is too large for the handoff: $relative" }
    $destination = Join-Path $source $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $path -Destination $destination
}

foreach ($file in @('.editorconfig', '.gitattributes', '.gitignore', 'Directory.Build.props',
    'global.json', 'README.md', 'SOURCE-HANDOFF.md', 'XboxControllerMouse.sln')) {
    Copy-SourceFile (Join-Path $repo $file) $file
}
$allowed = @{
    'src' = @('.cs', '.csproj', '.xaml', '.manifest', '.ico', '.png', '.md')
    'tests' = @('.cs', '.csproj')
    'docs' = @('.md', '.txt', '.json')
    'scripts' = @('.ps1')
    'installer' = @('.iss')
    'research' = @('.cs', '.csproj', '.md', '.txt')
    '.github' = @('.yml', '.yaml', '.md')
}
foreach ($directory in $allowed.Keys) {
    $folder = Join-Path $repo $directory
    if (!(Test-Path -LiteralPath $folder)) { throw "Missing source directory: $directory" }
    $items = Get-ChildItem -LiteralPath $folder -Recurse -Force
    if ($items | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) {
        throw "Linked content rejected in $directory. Review it before packaging."
    }
    foreach ($file in $items | Where-Object { !$_.PSIsContainer }) {
        $relative = [IO.Path]::GetRelativePath($repo, $file.FullName)
        if ($relative -match '(^|[\\/])(bin|obj|x64|\.git|\.vs|\.tools|artifacts|tmp)([\\/]|$)') { continue }
        if ($file.Extension.ToLowerInvariant() -notin $allowed[$directory]) { continue }
        if ($file.Name -match '^(settings|profiles|target-memory|motion-learning|credentials|secrets)\.json$') {
            throw "Local configuration must not be packaged: $relative"
        }
        Copy-SourceFile $file.FullName $relative
    }
}

if ($EvidenceDirectory) {
    $evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
    $destination = Join-Path $source 'docs\verification\2026-10-22'
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $records = @{
        'core-final-confirmed.txt' = 'core-final-confirmed.txt'
        'reviewed-acceptance\result.txt' = 'reviewed-acceptance\result.txt'
        'reviewed-acceptance\reflow.txt' = 'reviewed-acceptance\reflow.txt'
        'reviewed-acceptance\windows-text-size.txt' = 'reviewed-acceptance\windows-text-size.txt'
        'reviewed-acceptance\visual-access-palette-contrast.txt' = 'reviewed-acceptance\visual-access-palette-contrast.txt'
        'reviewed-native-keyboard\keyboard-input.txt' = 'reviewed-native-keyboard\keyboard-input.txt'
        'dependency-audit-final.json' = 'dependency-audit-final.json'
        'defender-reviewed.txt' = 'defender-reviewed.txt'
        'defender-source-final.txt' = 'defender-source-final.txt'
        'native-input\failure.txt' = 'native-input\failure.txt'
        'native-input-interactive\failure.txt' = 'native-input-interactive\failure.txt'
        'native-input-desktop\failure.txt' = 'native-input-desktop\failure.txt'
        'final-summary.json' = 'final-summary.json'
    }
    $privateRoots = @($repo, $repo.Replace('\', '/'), $env:USERPROFILE) | Where-Object { $_ }
    foreach ($record in $records.GetEnumerator()) {
        $path = Join-Path $evidence $record.Key
        if (!(Test-Path -LiteralPath $path)) { throw "Missing requested audit evidence: $($record.Key)" }
        $text = [IO.File]::ReadAllText($path)
        foreach ($privateRoot in $privateRoots) {
            $text = $text.Replace($privateRoot.Replace('\', '\\'), '<workspace>')
            $text = $text.Replace($privateRoot, '<workspace>')
        }
        $recordDestination = Join-Path $destination $record.Value
        New-Item -ItemType Directory -Path (Split-Path -Parent $recordDestination) -Force | Out-Null
        [IO.File]::WriteAllText($recordDestination, $text)
    }
    @'
# Audit evidence

These are sanitised records from the 22 October 2026 engineering audit, before the source-formatting handoff. They identify the audited executable; that executable is not included in this source ZIP. Paths have been replaced with <workspace>.

The native pointer record is an unresolved guarded integration failure, not a pass. The core, WPF, text, palette and native typing records passed. The source handoff's separate clean-build verification is recorded in HANDOFF-VERIFICATION.md at the repository root.
'@ | Set-Content -LiteralPath (Join-Path $destination 'README.md')
}
if (Test-Path -LiteralPath (Join-Path $repo 'HANDOFF-VERIFICATION.md')) {
    Copy-SourceFile (Join-Path $repo 'HANDOFF-VERIFICATION.md') 'HANDOFF-VERIFICATION.md'
}

$secretPattern = '(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,}|sk-(?:proj-)?[A-Za-z0-9_-]{24,}|AKIA[0-9A-Z]{16}|-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----)'
foreach ($file in Get-ChildItem -LiteralPath $source -Recurse -Force -File) {
    if ($file.Extension -in '.png', '.ico') { continue }
    if ([IO.File]::ReadAllText($file.FullName) -match $secretPattern) {
        throw "Possible credential found; package withheld: $([IO.Path]::GetRelativePath($source, $file.FullName))"
    }
}
$files = @(Get-ChildItem -LiteralPath $source -Recurse -Force -File | Sort-Object FullName)
$manifest = foreach ($file in $files) {
    $relative = [IO.Path]::GetRelativePath($source, $file.FullName).Replace('\', '/')
    "$((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $relative"
}
$manifest | Set-Content -LiteralPath (Join-Path $source 'SOURCE-MANIFEST.sha256')
[IO.Compression.ZipFile]::CreateFromDirectory($package, $zip, [IO.Compression.CompressionLevel]::Optimal, $false)
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    $entries = @($archive.Entries | Where-Object { !$_.FullName.EndsWith('/') })
    if ($entries.Count -ne $files.Count + 1) { throw 'ZIP file count differs from the staged source.' }
    foreach ($entry in $entries) {
        if (!$entry.FullName.StartsWith('Relay/') -or $entry.FullName -match '(^|/)(\.\.|\.git|bin|obj|\.tools|artifacts|tmp)(/|$)' -or
            [IO.Path]::GetExtension($entry.FullName) -in '.exe', '.dll', '.pdb', '.pfx', '.p12') {
            throw "Unexpected ZIP entry: $($entry.FullName)"
        }
        $stream = $entry.Open()
        try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() }
        finally { $stream.Dispose() }
        $staged = Join-Path $package $entry.FullName
        if ($hash -ne (Get-FileHash -LiteralPath $staged -Algorithm SHA256).Hash.ToLowerInvariant()) {
            throw "ZIP content differs from the staged file: $($entry.FullName)"
        }
    }
} finally { $archive.Dispose() }
$zipHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$zipHash  $name.zip" | Set-Content -LiteralPath (Join-Path $output "$name.sha256")
[pscustomobject]@{ Zip = $zip; SourceDirectory = $source; Files = $files.Count + 1; Bytes = (Get-Item -LiteralPath $zip).Length; SHA256 = $zipHash }
