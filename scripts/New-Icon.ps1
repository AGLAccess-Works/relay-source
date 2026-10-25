$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$target = Join-Path $PSScriptRoot '..\src\ControllerMouse.App\Assets\app.ico'
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($target))) | Out-Null
$images = @()
foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    $bitmap = [Drawing.Bitmap]::new($size, $size)
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.ScaleTransform($size / 256.0, $size / 256.0)
    $bg = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#202328'))
    $accent = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#FFB443'))
    $white = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#EEF0F4'))
    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc(20, 20, 80, 80, 180, 90)
    $path.AddArc(156, 20, 80, 80, 270, 90)
    $path.AddArc(156, 156, 80, 80, 0, 90)
    $path.AddArc(20, 156, 80, 80, 90, 90)
    $path.CloseFigure()
    $g.FillPath($white, $path)
    $outline = [Drawing.Pen]::new($bg, 18)
    $g.DrawEllipse($outline, 62, 85, 111, 111)
    $g.FillEllipse($accent, 165, 45, 40, 40)
    $stream = [IO.MemoryStream]::new()
    $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
    $images += ,@($size, $stream.ToArray())
    $stream.Dispose(); $outline.Dispose(); $path.Dispose(); $bg.Dispose(); $accent.Dispose(); $white.Dispose(); $g.Dispose(); $bitmap.Dispose()
}
$file = [IO.File]::Create([IO.Path]::GetFullPath($target))
$writer = [IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
    $offset = 6 + 16 * $images.Count
    foreach ($item in $images) {
        $dimension = if ($item[0] -eq 256) { 0 } else { $item[0] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([uint16]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$item[1].Length); $writer.Write([uint32]$offset)
        $offset += $item[1].Length
    }
    foreach ($item in $images) { $writer.Write([byte[]]$item[1]) }
} finally { $writer.Dispose(); $file.Dispose() }
Write-Output "Created $target"
