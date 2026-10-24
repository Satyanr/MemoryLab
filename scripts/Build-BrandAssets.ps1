[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$brandDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) 'Assets\Brand'
$reader = [System.Xml.XmlReader]::Create((Join-Path $brandDirectory 'LogoDrawing.xaml'))
try { $resources = [System.Windows.Markup.XamlReader]::Load($reader) }
finally { $reader.Dispose() }
$drawing = $resources['MemoryLabLogo'].Drawing

function Get-LogoPng([int] $Size) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    $context = $visual.RenderOpen()
    $context.PushTransform((New-Object System.Windows.Media.ScaleTransform ($Size / 512.0), ($Size / 512.0)))
    $context.DrawDrawing($drawing)
    $context.Pop()
    $context.Close()
    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap $Size, $Size, 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object System.IO.MemoryStream
    try {
        $encoder.Save($stream)
        return ,$stream.ToArray()
    }
    finally { $stream.Dispose() }
}

[System.IO.File]::WriteAllBytes((Join-Path $brandDirectory 'MemoryLab.png'), (Get-LogoPng 512))
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = @($sizes | ForEach-Object { ,(Get-LogoPng $_) })
$iconStream = [System.IO.File]::Create((Join-Path $brandDirectory 'MemoryLab.ico'))
$writer = New-Object System.IO.BinaryWriter $iconStream
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $dimension = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$index].Length)
        $writer.Write([uint32]$offset)
        $offset += $frames[$index].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
}
finally { $writer.Dispose() }

function Save-WizardBitmap([string] $Name, [int] $Width, [int] $Height, [bool] $Sidebar) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    $context = $visual.RenderOpen()
    $background = [System.Windows.Media.BrushConverter]::new().ConvertFromString('#0F1F2C')
    $context.DrawRectangle($background, $null, [System.Windows.Rect]::new(0, 0, $Width, $Height))
    if ($Sidebar) {
        $context.PushTransform([System.Windows.Media.TranslateTransform]::new(18, 36))
        $context.PushTransform([System.Windows.Media.ScaleTransform]::new(0.25, 0.25))
        $context.DrawDrawing($drawing)
        $context.Pop()
        $context.Pop()
        $text = [System.Windows.Media.FormattedText]::new('MemoryLab', [Globalization.CultureInfo]::InvariantCulture,
            [System.Windows.FlowDirection]::LeftToRight, [System.Windows.Media.Typeface]::new('Segoe UI Semibold'),
            22, [System.Windows.Media.Brushes]::White, 1.0)
        $context.DrawText($text, [System.Windows.Point]::new(18, 185))
        $subtitle = [System.Windows.Media.FormattedText]::new("Memory tools.`nMade simple.", [Globalization.CultureInfo]::InvariantCulture,
            [System.Windows.FlowDirection]::LeftToRight, [System.Windows.Media.Typeface]::new('Segoe UI'),
            12, [System.Windows.Media.BrushConverter]::new().ConvertFromString('#A9C1CE'), 1.0)
        $context.DrawText($subtitle, [System.Windows.Point]::new(20, 222))
    }
    else {
        $context.PushTransform([System.Windows.Media.ScaleTransform]::new($Width / 512.0, $Height / 512.0))
        $context.DrawDrawing($drawing)
        $context.Pop()
    }
    $context.Close()
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($Width, $Height, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [System.Windows.Media.Imaging.BmpBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [System.IO.File]::Create((Join-Path $brandDirectory $Name))
    try { $encoder.Save($stream) }
    finally { $stream.Dispose() }
}

Save-WizardBitmap 'InstallerSidebar.bmp' 164 314 $true
Save-WizardBitmap 'InstallerHeader.bmp' 64 64 $false
Write-Host 'Created MemoryLab.png and multi-resolution MemoryLab.ico from LogoDrawing.xaml.'
