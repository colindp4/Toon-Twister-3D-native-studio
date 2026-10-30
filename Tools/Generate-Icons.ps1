Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Drawing.Common -ErrorAction SilentlyContinue

$root = Split-Path -Parent $PSScriptRoot
$resources = Join-Path $root 'Resources'
New-Item -ItemType Directory -Path $resources -Force | Out-Null

function Add-RoundedRectangle([System.Drawing.Graphics]$graphics, [System.Drawing.Brush]$brush, [float]$x, [float]$y, [float]$width, [float]$height, [float]$radius) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $radius * 2
    $path.AddArc($x, $y, $diameter, $diameter, 180, 90)
    $path.AddArc($x + $width - $diameter, $y, $diameter, $diameter, 270, 90)
    $path.AddArc($x + $width - $diameter, $y + $height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($x, $y + $height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    $graphics.FillPath($brush, $path)
    $path.Dispose()
}

function New-IconBitmap([int]$size, [bool]$setup) {
    $bitmap = [System.Drawing.Bitmap]::new(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    $outer = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.Rectangle]::new(0,0,256,256), [System.Drawing.Color]::FromArgb(16,35,55), [System.Drawing.Color]::FromArgb(16,112,123), 315)
    Add-RoundedRectangle $graphics $outer 5 5 246 246 53
    $outer.Dispose()
    $glow = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(38,93,221,207))
    $graphics.FillEllipse($glow, 24, 20, 210, 210)
    $glow.Dispose()

    # Cream editor frame and a deep teal 3-D preview window.
    $frameBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.Rectangle]::new(35,51,186,169), [System.Drawing.Color]::White, [System.Drawing.Color]::FromArgb(213,235,220), 90)
    Add-RoundedRectangle $graphics $frameBrush 35 51 186 169 17
    $frameBrush.Dispose()
    $screenBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.Rectangle]::new(49,78,158,100), [System.Drawing.Color]::FromArgb(42,108,130), [System.Drawing.Color]::FromArgb(16,43,59), 90)
    Add-RoundedRectangle $graphics $screenBrush 49 78 158 100 8
    $screenBrush.Dispose()

    # A small stylized 3-D cube: three colored faces with a crisp outline.
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(12,39,51), 5)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $top = [System.Drawing.Point[]]@([System.Drawing.Point]::new(102,105),[System.Drawing.Point]::new(132,89),[System.Drawing.Point]::new(162,105),[System.Drawing.Point]::new(132,122))
    $left = [System.Drawing.Point[]]@([System.Drawing.Point]::new(102,105),[System.Drawing.Point]::new(132,122),[System.Drawing.Point]::new(132,157),[System.Drawing.Point]::new(102,139))
    $right = [System.Drawing.Point[]]@([System.Drawing.Point]::new(132,122),[System.Drawing.Point]::new(162,105),[System.Drawing.Point]::new(162,139),[System.Drawing.Point]::new(132,157))
    $graphics.FillPolygon([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255,190,72)), $top)
    $graphics.FillPolygon([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(55,207,187)), $left)
    $graphics.FillPolygon([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(26,147,157)), $right)
    $graphics.DrawPolygon($pen, $top); $graphics.DrawPolygon($pen, $left); $graphics.DrawPolygon($pen, $right)
    $pen.Dispose()

    # Timeline tracks and play marker make the app's purpose legible at small sizes.
    $track1 = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(101,82,197))
    $track2 = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(47,176,181))
    $track3 = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255,174,64))
    Add-RoundedRectangle $graphics $track1 52 190 95 10 5
    Add-RoundedRectangle $graphics $track2 52 205 137 8 4
    Add-RoundedRectangle $graphics $track3 52 218 80 7 3
    $track1.Dispose(); $track2.Dispose(); $track3.Dispose()

    # Clapper accent at the upper edge of the frame.
    $clapper = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.Rectangle]::new(43,31,172,39), [System.Drawing.Color]::FromArgb(255,194,79), [System.Drawing.Color]::FromArgb(255,129,52), 0)
    $clapperPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $clapperPath.AddPolygon([System.Drawing.Point[]]@([System.Drawing.Point]::new(43,51),[System.Drawing.Point]::new(58,31),[System.Drawing.Point]::new(215,31),[System.Drawing.Point]::new(201,51)))
    $graphics.FillPath($clapper, $clapperPath)
    $graphics.DrawPath([System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(12,39,51),4), $clapperPath)
    for ($i=0; $i -lt 4; $i++) {
        $x = 65 + $i * 38
        $graphics.DrawLine([System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(238,246,221),5), $x, 34, $x-9, 48)
    }
    $clapperPath.Dispose(); $clapper.Dispose()

    if ($setup) {
        $badge = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(124,213,105))
        $badgeEdge = [System.Drawing.Pen]::new([System.Drawing.Color]::White, 7)
        $graphics.FillEllipse($badge, 167, 164, 75, 75)
        $graphics.DrawEllipse($badgeEdge, 167, 164, 75, 75)
        $badge.Dispose(); $badgeEdge.Dispose()
        $arrowPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(18,54,61), 9)
        $arrowPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $arrowPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $arrowPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $graphics.DrawLine($arrowPen, 204, 182, 204, 215)
        $graphics.DrawLine($arrowPen, 190, 202, 204, 216)
        $graphics.DrawLine($arrowPen, 218, 202, 204, 216)
        $arrowPen.Dispose()
    }

    $scaled = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $smallGraphics = [System.Drawing.Graphics]::FromImage($scaled)
    $smallGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $smallGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $smallGraphics.DrawImage($bitmap, 0, 0, $size, $size)
    $smallGraphics.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
    return $scaled
}

function Save-Icon([string]$path, [bool]$setup) {
    $sizes = @(256,128,64,48,32,16)
    $images = @()
    foreach ($size in $sizes) {
        $bitmap = New-IconBitmap $size $setup
        $stream = [System.IO.MemoryStream]::new()
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $images += ,$stream.ToArray()
        $stream.Dispose(); $bitmap.Dispose()
    }
    $file = [System.IO.File]::Create($path)
    $writer = [System.IO.BinaryWriter]::new($file)
    $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i=0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { [byte]0 } else { [byte]$sizes[$i] }
        $writer.Write($dimension); $writer.Write($dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([UInt16]1); $writer.Write([UInt16]32); $writer.Write([UInt32]$images[$i].Length); $writer.Write([UInt32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($image in $images) { $writer.Write($image) }
    $writer.Dispose(); $file.Dispose()
}

Save-Icon (Join-Path $resources 'ToonTwister.ico') $false
Save-Icon (Join-Path $resources 'ToonTwisterSetup.ico') $true
Write-Output "Created app and setup icons in $resources"
