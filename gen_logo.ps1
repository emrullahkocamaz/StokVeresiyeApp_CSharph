Add-Type -AssemblyName System.Drawing

$srcDir = "c:\Users\emrullah.kocamaz.ASEKER\Downloads\StokVeresiyeApp_CSharph\StokVeresiyeApp"
$resDir = Join-Path $srcDir "Resources"
if (-not (Test-Path $resDir)) { New-Item -ItemType Directory -Path $resDir }

# 256x256 Logo Çiz
$sz = 256
$bmp = New-Object System.Drawing.Bitmap($sz, $sz)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

# Arka Plan Degrade Kart (Lacivert -> Kraliyet Mavisi)
$rect = New-Object System.Drawing.RectangleF(16, 16, 224, 224)
$c1 = [System.Drawing.Color]::FromArgb(15, 23, 42)
$c2 = [System.Drawing.Color]::FromArgb(37, 99, 235)
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $c1, $c2, [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)

$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$radius = 48
$path.AddArc($rect.X, $rect.Y, $radius * 2, $radius * 2, 180, 90)
$path.AddArc($rect.Right - $radius * 2, $rect.Y, $radius * 2, $radius * 2, 270, 90)
$path.AddArc($rect.Right - $radius * 2, $rect.Bottom - $radius * 2, $radius * 2, $radius * 2, 0, 90)
$path.AddArc($rect.X, $rect.Bottom - $radius * 2, $radius * 2, $radius * 2, 90, 90)
$path.CloseFigure()
$g.FillPath($brush, $path)

# Beyaz Çerçeve Parıltısı
$penGlow = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(50, 255, 255, 255), 4)
$g.DrawPath($penGlow, $path)

# Beyaz 'B' Harfi
$font = New-Object System.Drawing.Font("Segoe UI", 120, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$sf = New-Object System.Drawing.StringFormat
$sf.Alignment = [System.Drawing.StringAlignment]::Center
$sf.LineAlignment = [System.Drawing.StringAlignment]::Center
$g.DrawString("B", $font, $textBrush, (New-Object System.Drawing.RectangleF(0, 0, $sz, $sz)), $sf)

# Sağ Alt Yeşil Başarı Rozeti (Online / Aktif)
$bDot = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(52, 211, 153))
$g.FillEllipse($bDot, 172, 172, 48, 48)
$penDot = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 6)
$g.DrawEllipse($penDot, 172, 172, 48, 48)

$g.Dispose()

# PNG kaydet
$pngPath = Join-Path $resDir "app_icon_square.png"
$bmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

# ICO olarak kaydet
$hIcon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)
$icoPath = Join-Path $resDir "app.ico"
$fs = [System.IO.File]::OpenWrite($icoPath)
$icon.Save($fs)
$fs.Close()

Write-Host "Logo & ICO üretildi: $icoPath"
