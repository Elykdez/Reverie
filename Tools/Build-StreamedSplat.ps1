param(
    [string]$Source = 'Assets/SceneData/Tower/Source/VictoriaTower.ply',
    [string]$Output = 'Assets/StreamingAssets/Scenes/Tower/Splats',
    [ValidateRange(1, 8)][int]$Levels = 6,
    [ValidateSet('auto', 'cpu')][string]$DecimationDevice = 'auto'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = [IO.Path]::GetFullPath($(if ([IO.Path]::IsPathRooted($Source)) { $Source } else { Join-Path $projectRoot $Source }))
$outputPath = [IO.Path]::GetFullPath($(if ([IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $projectRoot $Output }))
$version = '3.4.2'
$buildName = [IO.Path]::GetFileNameWithoutExtension($sourcePath)
$scratch = Join-Path $projectRoot "Library/StreamedSplats/$buildName"
$staging = Join-Path $scratch 'Streamed'

if (!(Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "Source not found: $sourcePath" }
New-Item -ItemType Directory -Force -Path $scratch, $staging, $outputPath | Out-Null
$timer = [Diagnostics.Stopwatch]::StartNew()
$sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash

function Invoke-SplatTransform([string[]]$Arguments) {
    & npx.cmd --yes "@playcanvas/splat-transform@$version" --no-tty @Arguments
    if ($LASTEXITCODE -ne 0) { throw "splat-transform failed ($LASTEXITCODE)." }
}

# Cache intermediate PLYs outside Assets so Unity never imports the LOD pyramid.
$cacheKey = "$sourceHash/$version/$Levels"
$cacheFile = Join-Path $scratch 'source-key.txt'
$reuse = (Test-Path -LiteralPath $cacheFile) -and ((Get-Content -LiteralPath $cacheFile -Raw).Trim() -eq $cacheKey)
$inputs = @($sourcePath, '--tag-lod', '0')
$previous = $sourcePath
for ($level = 1; $level -lt $Levels; $level++) {
    $ply = Join-Path $scratch "lod$level.ply"
    if (!$reuse -or !(Test-Path -LiteralPath $ply)) {
        Write-Host "Building LOD $level (1/$([Math]::Pow(2, $level)) of the source splat count)..."
        $decimationArguments = @('-w', $previous, '--filter-nan', '--decimate', '50%', $ply)
        if ($DecimationDevice -eq 'cpu') { $decimationArguments += @('--gpu', 'cpu') }
        Invoke-SplatTransform $decimationArguments
    }
    $inputs += @($ply, '--tag-lod', "$level")
    $previous = $ply
}
Set-Content -LiteralPath $cacheFile -Value $cacheKey -Encoding ASCII

# Use PlayCanvas' spatial tree and compressed SOG chunks; retain all source SH bands.
Invoke-SplatTransform (@('-w', '--lod-chunk-count', '128', '--lod-chunk-extent', '16') + $inputs + @((Join-Path $staging 'lod-meta.json'), '--filter-nan'))
$metadata = Get-Content -LiteralPath (Join-Path $staging 'lod-meta.json') -Raw | ConvertFrom-Json
if ($metadata.version -ne 1 -or $metadata.lodLevels -ne $Levels -or !$metadata.filenames) {
    throw 'Conversion did not produce the requested LOD pyramid.'
}
foreach ($filename in $metadata.filenames) {
    $chunkPath = Join-Path $staging $filename
    $chunk = Get-Content -LiteralPath $chunkPath -Raw | ConvertFrom-Json
    foreach ($property in $chunk.PSObject.Properties) {
        foreach ($texture in $property.Value.files) {
            if (!(Test-Path -LiteralPath (Join-Path (Split-Path $chunkPath) $texture) -PathType Leaf)) {
                throw "Missing chunk texture: $filename / $texture"
            }
        }
    }
}
foreach ($filename in $metadata.filenames) {
    Copy-Item -LiteralPath (Split-Path (Join-Path $staging $filename)) -Destination $outputPath -Recurse -Force
}
Copy-Item -LiteralPath (Join-Path $staging 'lod-meta.json') -Destination $outputPath -Force

# The editor can show the whole scene without importing the full-resolution PLY.
Invoke-SplatTransform @('-w', $previous, (Join-Path $outputPath 'preview.sog'))

$report = [ordered]@{
    tool = "@playcanvas/splat-transform@$version"
    source = $Source
    sourceSha256 = $sourceHash
    levels = $Levels
    reductionPerLevel = 0.5
    decimationDevice = $DecimationDevice
    chunkCountK = 128
    chunkExtent = 16
    sphericalHarmonics = 'preserved'
    splatCounts = $metadata.counts
    durationSeconds = [Math]::Round($timer.Elapsed.TotalSeconds, 1)
    generatedUtc = [DateTime]::UtcNow.ToString('o')
}
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputPath 'build-info.json') -Encoding ASCII
Write-Host "Streamed splat ready: $outputPath ($($report.durationSeconds)s)"
