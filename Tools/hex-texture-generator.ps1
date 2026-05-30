param(
    [Parameter(Mandatory = $true)][string]$HexFile,
    [Parameter(Mandatory = $true)][int]$Width,
    [Parameter(Mandatory = $true)][int]$Height,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [string]$Format = "png",
    [string]$TransparentHex = "7F7F7F"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $HexFile)) {
    throw "Hex file not found: $HexFile"
}

if ($Width -le 0 -or $Height -le 0) {
    throw "Invalid size: $Width x $Height"
}

Add-Type -AssemblyName System.Drawing

$raw = Get-Content -Path $HexFile -Raw
$tokens = $raw -split "[\s,]+" | Where-Object { $_ -ne "" }
$expected = $Width * $Height
if ($tokens.Count -ne $expected) {
    throw "Hex count mismatch. Expected $expected, got $($tokens.Count)."
}

$transparent = $TransparentHex.Trim().TrimStart("#").ToUpperInvariant()
$bitmap = New-Object System.Drawing.Bitmap -ArgumentList @(
    $Width,
    $Height,
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
)

for ($y = 0; $y -lt $Height; $y++) {
    for ($x = 0; $x -lt $Width; $x++) {
        $idx = $y * $Width + $x
        $hex = $tokens[$idx].Trim().TrimStart("#").ToUpperInvariant()
        if ($hex.Length -ne 6) {
            throw "Invalid hex value at index ${idx}: ${hex}"
        }
        $r = [Convert]::ToInt32($hex.Substring(0, 2), 16)
        $g = [Convert]::ToInt32($hex.Substring(2, 2), 16)
        $b = [Convert]::ToInt32($hex.Substring(4, 2), 16)
        $a = if ($hex -eq $transparent) { 0 } else { 255 }
        $color = [System.Drawing.Color]::FromArgb($a, $r, $g, $b)
        $bitmap.SetPixel($x, $y, $color)
    }
}

$dir = Split-Path -Path $OutputPath
if ($dir -and -not (Test-Path $dir)) {
    New-Item -ItemType Directory -Path $dir | Out-Null
}

switch ($Format.ToLowerInvariant()) {
    "png" { $imgFormat = [System.Drawing.Imaging.ImageFormat]::Png }
    "bmp" { $imgFormat = [System.Drawing.Imaging.ImageFormat]::Bmp }
    "jpg" { $imgFormat = [System.Drawing.Imaging.ImageFormat]::Jpeg }
    "jpeg" { $imgFormat = [System.Drawing.Imaging.ImageFormat]::Jpeg }
    default { throw "Unsupported format: $Format" }
}

$bitmap.Save($OutputPath, $imgFormat)
$bitmap.Dispose()

Write-Host "Saved: $OutputPath"
