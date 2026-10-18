# Builds the installer's wizard pictures from Zerg's 1024 px icon master
# (assets/zerg-1024.png, made by make-icon.ps1), into packaging/wizard/:
#
#   large-<w>.png   the Welcome and Finished pages' left panel, 164:314, the
#                   icon tile centred on transparency so the panel's own colour
#                   (dark or light, by Windows' mode) shows round it
#   small-<s>.png   the other pages' top-right corner: the tile alone
#
# One file per size Inno Setup lays those areas out at, 100 % to 250 % scale
# (its help, WizardImageFile and WizardSmallImageFile); setup picks the
# nearest. packaging/Zerg.iss names every file.
#
#   powershell -ExecutionPolicy Bypass -File tools/make-wizard-images.ps1
#
# Kept ASCII.

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'packaging\wizard'
New-Item -ItemType Directory -Force $out | Out-Null

Add-Type -AssemblyName PresentationCore, WindowsBase
$master = [Windows.Media.Imaging.BitmapDecoder]::Create(
    [Uri](Join-Path $root 'assets\zerg-1024.png'), 'None', 'OnLoad').Frames[0]

function Save([int]$W, [int]$H, [double]$Side, [string]$Name) {
    $dv = New-Object Windows.Media.DrawingVisual
    [Windows.Media.RenderOptions]::SetBitmapScalingMode($dv, 'HighQuality')
    $dc = $dv.RenderOpen()
    # Whole pixels, so the tile's edge is not smeared across two.
    $s = [Math]::Round($Side)
    $x = [Math]::Round(($W - $s) / 2)
    $y = [Math]::Round(($H - $s) / 2)
    $dc.DrawImage($master, (New-Object Windows.Rect($x, $y, $s, $s)))
    $dc.Close()
    $rtb = New-Object Windows.Media.Imaging.RenderTargetBitmap($W, $H, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($dv)
    $enc = New-Object Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($rtb))
    $file = [IO.File]::Create((Join-Path $out $Name))
    try { $enc.Save($file) } finally { $file.Close() }
    "$Name  ${W}x${H}"
}

# Left panel: the tile across 60 % of the panel's width.
foreach ($size in @(@(202, 386), @(269, 515), @(336, 643), @(403, 772), @(430, 824), @(498, 953), @(534, 1022))) {
    Save $size[0] $size[1] ($size[0] * 0.6) "large-$($size[0]).png"
}
# Corner: the tile with a small margin, as Inno's own corner picture has.
foreach ($s in @(58, 77, 97, 116, 124, 143, 159)) {
    Save $s $s ($s * 0.9) "small-$s.png"
}
