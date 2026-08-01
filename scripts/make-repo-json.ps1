param(
    [Parameter(Mandatory = $true)]
    [string] $Owner,

    [Parameter(Mandatory = $true)]
    [string] $Repository,

    [Parameter(Mandatory = $true)]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [string] $Tag,

    [Parameter(Mandatory = $true)]
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'

$assemblyVersion = if ($Version -match '^\d+\.\d+\.\d+$') {
    "$Version.0"
}
elseif ($Version -match '^\d+\.\d+\.\d+\.\d+$') {
    $Version
}
else {
    throw "Version must contain three or four numeric components: $Version"
}

$downloadBaseUrl = "https://downloads.miqote69.com/crescent-atlas/$Tag"
$repoUrl = "https://github.com/$Owner/$Repository"
$lastUpdate = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()

$entry = [ordered]@{
    Author = $Owner
    Name = 'Crescent Atlas'
    Description = 'Display-only Occult Crescent atlas with treasure spot checks and field markers, FATE/CE timers and progress, Magic Pot prediction and alerts, carrot and Forked Tower markers, aetherytes, visit history, and local observation collection. It does not automate movement or interaction.'
    InternalName = 'CrescentAtlas'
    AssemblyVersion = $assemblyVersion
    TestingAssemblyVersion = $assemblyVersion
    RepoUrl = $repoUrl
    IconUrl = "https://raw.githubusercontent.com/$Owner/$Repository/main/images/icon-v44.png"
    ApplicableVersion = 'any'
    DalamudApiLevel = 15
    TestingDalamudApiLevel = 15
    Punchline = 'Occult Crescent map with treasure guides, event tracking and Magic Pot predictions.'
    Tags = @(
        'occult crescent'
        'map'
        'notifications'
    )
    MinimumDalamudVersion = '15.0.0.0'
    IsHide = $false
    IsTestingExclusive = $false
    DownloadLinkInstall = "$downloadBaseUrl/install"
    DownloadLinkTesting = "$downloadBaseUrl/testing"
    DownloadLinkUpdate = "$downloadBaseUrl/update"
    LastUpdate = $lastUpdate
}

$absoluteOutputPath = [IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $absoluteOutputPath
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$json = ConvertTo-Json -InputObject @($entry) -Depth 5
[IO.File]::WriteAllText($absoluteOutputPath, $json, [Text.UTF8Encoding]::new($false))
