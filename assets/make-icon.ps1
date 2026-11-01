Add-Type -AssemblyName System.Drawing

$out = "C:\Users\Admin\Documents\Proojeto VR Dead Space\meta-horizon-link-doctor\assets\icon.ico"

function New-LogoBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = $size / 256.0

    # fundo arredondado
    $r = [int](58 * $s)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $w = $size - 4
    $path.AddArc(2, 2, $d, $d, 180, 90)
    $path.AddArc(2 + $w - $d, 2, $d, $d, 270, 90)
    $path.AddArc(2 + $w - $d, 2 + $w - $d, $d, $d, 0, 90)
    $path.AddArc(2, 2 + $w - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point(0, 0)),
        (New-Object System.Drawing.Point($size, $size)),
        [System.Drawing.Color]::FromArgb(255, 24, 36, 54),
        [System.Drawing.Color]::FromArgb(255, 12, 18, 28))
    $g.FillPath($bg, $path)

    # "infinito" (dois lacos, estilo Meta)
    $penW = [float](17 * $s)
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, $penW)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $lw = [float](86 * $s); $lh = [float](96 * $s)
    $g.DrawEllipse($pen, [float](50 * $s), [float](76 * $s), $lw, $lh)
    $g.DrawEllipse($pen, [float](120 * $s), [float](76 * $s), $lw, $lh)

    # selo verde com check
    $cr = [float](52 * $s)
    $cx = [float](188 * $s); $cy = [float](188 * $s)
    $green = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 32, 168, 96))
    $g.FillEllipse($green, $cx - $cr / 2, $cy - $cr / 2, $cr, $cr)

    $checkPen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, [float](11 * $s))
    $checkPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $checkPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLines($checkPen, @(
        (New-Object System.Drawing.PointF([float](166 * $s), [float](188 * $s))),
        (New-Object System.Drawing.PointF([float](181 * $s), [float](203 * $s))),
        (New-Object System.Drawing.PointF([float](210 * $s), [float](172 * $s)))
    ))

    $g.Dispose()
    return $bmp
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs = @()

foreach ($sz in $sizes) {
    $bmp = New-LogoBitmap $sz
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,@($sz, $ms.ToArray())
    $ms.Dispose()
    $bmp.Dispose()
}

$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter($fs)

$bw.Write([uint16]0)                 # reserved
$bw.Write([uint16]1)                 # type = icon
$bw.Write([uint16]$pngs.Count)       # count

$offset = 6 + (16 * $pngs.Count)
foreach ($entry in $pngs) {
    $sz = $entry[0]
    $data = $entry[1]
    $bw.Write([byte]($(if ($sz -ge 256) { 0 } else { $sz })))
    $bw.Write([byte]($(if ($sz -ge 256) { 0 } else { $sz })))
    $bw.Write([byte]0)               # palette
    $bw.Write([byte]0)               # reserved
    $bw.Write([uint16]1)             # planes
    $bw.Write([uint16]32)            # bpp
    $bw.Write([uint32]$data.Length)
    $bw.Write([uint32]$offset)
    $offset += $data.Length
}

foreach ($entry in $pngs) { $bw.Write($entry[1]) }

$bw.Flush(); $bw.Close(); $fs.Close()

Get-Item $out | Select-Object Name, Length | Format-List | Out-String
