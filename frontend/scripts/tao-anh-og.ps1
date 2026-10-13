<#
  Tao anh chia se mang xa hoi (Open Graph) va icon iOS cho frontend.
  Ghi vao public/: og-image.png (1200x630), apple-touch-icon.png (180x180).
  Chay: powershell -ExecutionPolicy Bypass -File scripts/tao-anh-og.ps1
#>
param([string]$OutDir = (Join-Path $PSScriptRoot '..\public'))
Add-Type -AssemblyName System.Drawing

$teal = [System.Drawing.ColorTranslator]::FromHtml('#21A691')
$deep = [System.Drawing.ColorTranslator]::FromHtml('#27403E')
$lime = [System.Drawing.ColorTranslator]::FromHtml('#87DF2C')
$white = [System.Drawing.Color]::White

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

# Logo giong public/icon.svg (khung 512), ve tai (x, y) voi canh $size
function Draw-Logo([System.Drawing.Graphics]$g, [float]$x, [float]$y, [float]$size) {
    $k = $size / 512
    $g.FillPath((New-Object System.Drawing.SolidBrush $teal), (New-RoundedRect $x $y $size $size (112 * $k)))
    $frame = New-Object System.Drawing.Pen $white, (26 * $k)
    $g.DrawPath($frame, (New-RoundedRect ($x + 112 * $k) ($y + 112 * $k) (288 * $k) (288 * $k) (40 * $k)))
    $line = New-Object System.Drawing.Pen $white, (22 * $k)
    foreach ($v in 208, 304) {
        $g.DrawLine($line, $x + $v * $k, $y + 112 * $k, $x + $v * $k, $y + 400 * $k)
        $g.DrawLine($line, $x + 112 * $k, $y + $v * $k, $x + 400 * $k, $y + $v * $k)
    }
    $g.FillPath((New-Object System.Drawing.SolidBrush $lime), (New-RoundedRect ($x + 318 * $k) ($y + 222 * $k) (68 * $k) (68 * $k) (12 * $k)))
}

function Save-Png([System.Drawing.Bitmap]$bmp, [string]$name) {
    $bmp.Save((Join-Path $OutDir $name), [System.Drawing.Imaging.ImageFormat]::Png)
}

# ---------- og-image.png ----------
$bmp = New-Object System.Drawing.Bitmap 1200, 630
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

$bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 0, 0, 1200, 630), $deep, $teal, 20
$g.FillRectangle($bg, 0, 0, 1200, 630)
# Hoa van o luoi mo phia sau
$gridPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(22, 255, 255, 255)), 2
for ($x = 0; $x -le 1200; $x += 60) { $g.DrawLine($gridPen, $x, 0, $x, 630) }
for ($y = 0; $y -le 630; $y += 60) { $g.DrawLine($gridPen, 0, $y, 1200, $y) }
$g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(70, 135, 223, 44))), 960, 120, 60, 60)
$g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(40, 135, 223, 44))), 1080, 420, 60, 60)

Draw-Logo $g 90 195 240

$whiteBrush = New-Object System.Drawing.SolidBrush $white
$softBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(225, 236, 248, 245))
$g.DrawString('Đồng Nai GridSmart', (New-Object System.Drawing.Font 'Segoe UI', 64, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)), $whiteBrush, 370, 190)
$g.DrawString('Phản ánh hiện trường · Quản lý đô thị theo ô lưới', (New-Object System.Drawing.Font 'Segoe UI Semibold', 32, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)), $whiteBrush, 374, 285)
$g.DrawString('Chụp ảnh, gửi vị trí và theo dõi tiến độ xử lý bằng mã tra cứu.', (New-Object System.Drawing.Font 'Segoe UI', 26, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)), $softBrush, 376, 345)
$g.FillRectangle((New-Object System.Drawing.SolidBrush $lime), 378, 418, 64, 6)
$g.DrawString('Thành phố Đồng Nai', (New-Object System.Drawing.Font 'Segoe UI Semibold', 24, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)), $whiteBrush, 374, 436)
$g.Dispose()
Save-Png $bmp 'og-image.png'
$bmp.Dispose()

# ---------- apple-touch-icon.png (iOS khong dung SVG) ----------
$icon = New-Object System.Drawing.Bitmap 180, 180
$g = [System.Drawing.Graphics]::FromImage($icon)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear($teal)   # iOS tu bo goc, nen phu kin mau nen
Draw-Logo $g 0 0 180
$g.Dispose()
Save-Png $icon 'apple-touch-icon.png'
$icon.Dispose()

Write-Output "Da tao og-image.png va apple-touch-icon.png tai $OutDir"
