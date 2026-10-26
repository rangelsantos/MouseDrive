# Draws the MouseDrive icon (a steering wheel on a dark badge) and writes MouseDrive.ico, with 16-64 px
# as 32-bit BMP frames (what the shell and WinForms read best at small sizes) and 256 px as PNG, plus
# mousedrive-icon-512.png for the README. Every size is drawn from vectors, so small ones stay crisp.
# Run it again after changing the drawing:  powershell -File make-icon.ps1
Add-Type -AssemblyName System.Drawing

$dir = $PSScriptRoot
$sizes = 16, 20, 24, 32, 40, 48, 64, 256

function Color([int]$r, [int]$g, [int]$b, [int]$a = 255) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }

function RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$rad) {
	$p = [System.Drawing.Drawing2D.GraphicsPath]::new()
	$d = $rad * 2
	$p.AddArc($x, $y, $d, $d, 180, 90)
	$p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
	$p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
	$p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
	$p.CloseFigure()
	return $p
}

# Drawn on a 64 x 64 grid and scaled. Strokes are a little heavier than a big drawing would want,
# so the wheel still reads at 16 px.
function Render([int]$n) {
	$b = [System.Drawing.Bitmap]::new($n, $n, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
	$g = [System.Drawing.Graphics]::FromImage($b)
	$g.SmoothingMode = 'AntiAlias'
	$g.PixelOffsetMode = 'HighQuality'
	$g.ScaleTransform($n / 64.0, $n / 64.0)

	$badge = RoundedRect 2 2 60 60 14
	$g.FillPath([System.Drawing.SolidBrush]::new((Color 30 30 38)), $badge)
	$g.DrawPath([System.Drawing.Pen]::new((Color 255 255 255 28), 1.2), $badge)

	$accent = Color 90 154 214
	$r = 21
	$rim = [System.Drawing.Pen]::new($accent, 7)
	$g.DrawEllipse($rim, 32 - $r, 32 - $r, $r * 2, $r * 2)
	$spoke = [System.Drawing.Pen]::new($accent, 6)
	$spoke.StartCap = 'Round'; $spoke.EndCap = 'Round'
	$g.DrawLine($spoke, 12, 33, 25, 33)
	$g.DrawLine($spoke, 39, 33, 52, 33)
	$g.DrawLine($spoke, 32, 39, 32, 51)
	$g.FillEllipse([System.Drawing.SolidBrush]::new($accent), 24.5, 25.5, 15, 15)
	$g.FillEllipse([System.Drawing.SolidBrush]::new((Color 30 30 38)), 29, 30, 6, 6)
	# The twelve o'clock stripe of a racing wheel.
	$mark = [System.Drawing.Pen]::new((Color 255 190 60), 7)
	$g.DrawArc($mark, 32 - $r, 32 - $r, $r * 2, $r * 2, 258, 24)

	$g.Dispose()
	return $b
}

function Frame([System.Drawing.Bitmap]$b) {
	$n = $b.Width
	$ms = [System.IO.MemoryStream]::new()
	if ($n -ge 256) {
		$b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
		return , $ms.ToArray()
	}
	$data = $b.LockBits([System.Drawing.Rectangle]::new(0, 0, $n, $n), 'ReadOnly', 'Format32bppArgb')
	$px = [byte[]]::new($n * $n * 4)
	[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $px, 0, $px.Length)
	$b.UnlockBits($data)
	$w = [System.IO.BinaryWriter]::new($ms)
	# BITMAPINFOHEADER; the height counts the color rows and the AND mask.
	$w.Write([int]40); $w.Write([int]$n); $w.Write([int]($n * 2)); $w.Write([int16]1); $w.Write([int16]32)
	for ($i = 0; $i -lt 6; $i++) { $w.Write([int]0) }
	for ($y = $n - 1; $y -ge 0; $y--) { $w.Write($px, $y * $n * 4, $n * 4) }
	$w.Write([byte[]]::new([Math]::Floor(($n + 31) / 32) * 4 * $n))
	$w.Flush()
	return , $ms.ToArray()
}

$frames = @()
foreach ($n in $sizes) {
	$b = Render $n
	$frames += , (Frame $b)
	$b.Dispose()
}

$out = [System.IO.MemoryStream]::new()
$w = [System.IO.BinaryWriter]::new($out)
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
	$n = $sizes[$i]
	$dim = [byte]($n % 256)
	$w.Write($dim); $w.Write($dim); $w.Write([byte]0); $w.Write([byte]0)
	$w.Write([int16]1); $w.Write([int16]32); $w.Write([int]$frames[$i].Length); $w.Write([int]$offset)
	$offset += $frames[$i].Length
}
foreach ($f in $frames) { $w.Write($f) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $dir 'MouseDrive.ico'), $out.ToArray())

$big = Render 512
$big.Save((Join-Path $dir 'mousedrive-icon-512.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$big.Dispose()
'MouseDrive.ico: ' + $out.Length + ' bytes'
