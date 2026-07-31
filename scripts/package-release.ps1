param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Configuration = 'Release',

    [string]$OutputPath = 'CrescentAtlas.zip'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$buildDirectory = Join-Path $projectRoot "bin\$Configuration"
$manifestPath = Join-Path $buildDirectory 'CrescentAtlas.json'
$assemblyPath = Join-Path $buildDirectory 'CrescentAtlas.dll'
$dependenciesPath = Join-Path $buildDirectory 'CrescentAtlas.deps.json'
$japanesePotAlertPath = Join-Path $buildDirectory 'CrescentAtlas.PotAlert.ja.wav'

foreach ($requiredPath in @(
    $manifestPath,
    $assemblyPath,
    $dependenciesPath,
    $japanesePotAlertPath
)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required build output is missing: $requiredPath"
    }
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.InternalName -ne 'CrescentAtlas') {
    throw "Generated manifest has an invalid InternalName: $($manifest.InternalName)"
}
if ($manifest.DalamudApiLevel -ne 15) {
    throw "Generated manifest has an invalid DalamudApiLevel: $($manifest.DalamudApiLevel)"
}
if ($manifest.AssemblyVersion -ne "$Version.0") {
    throw "Generated manifest version $($manifest.AssemblyVersion) does not match $Version.0"
}

$resolvedOutput = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputPath))
if (-not $resolvedOutput.StartsWith(
        $projectRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Output path must stay inside the project directory: $resolvedOutput"
}
if (Test-Path -LiteralPath $resolvedOutput) {
    Remove-Item -LiteralPath $resolvedOutput -Force
}

Compress-Archive -LiteralPath @(
    $assemblyPath,
    $dependenciesPath,
    $manifestPath,
    $japanesePotAlertPath
) -DestinationPath $resolvedOutput

$archive = [IO.Compression.ZipFile]::OpenRead($resolvedOutput)
try {
    $entryNames = @($archive.Entries | ForEach-Object FullName)
    $expectedNames = @(
        'CrescentAtlas.dll',
        'CrescentAtlas.deps.json',
        'CrescentAtlas.json',
        'CrescentAtlas.PotAlert.ja.wav'
    )
    if (Compare-Object -ReferenceObject $expectedNames -DifferenceObject $entryNames) {
        throw "Release archive contains unexpected entries: $($entryNames -join ', ')"
    }
}
finally {
    $archive.Dispose()
}

$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $resolvedOutput
[pscustomobject]@{
    Path = $resolvedOutput
    Version = $manifest.AssemblyVersion
    Bytes = (Get-Item -LiteralPath $resolvedOutput).Length
    Sha256 = $hash.Hash
}
