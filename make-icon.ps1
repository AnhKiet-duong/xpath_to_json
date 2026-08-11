# make-icon.ps1
# Sinh Assets\app-icon.ico (nhiều kích thước 16-256) từ file PNG nguồn,
# dùng làm icon của exe (ApplicationIcon) và icon cửa sổ WPF (Window.Icon).
#
# Cách dùng:  powershell -ExecutionPolicy Bypass -File make-icon.ps1
#   -Source: đường dẫn PNG nguồn (mặc định Untitled design.png)
#   -OutDir: thư mục đích cho app-icon.ico

param(
    [string]$Source = "$PSScriptRoot\Untitled design.png",
    [string]$OutDir  = "$PSScriptRoot\XPathScanner\XPathScanner.App\Assets"
)

Add-Type -AssemblyName System.Drawing

$sizes = @(16, 24, 32, 48, 64, 128, 256)

# ─── Xoá nền trắng (flood-fill từ biên) ──────────────────────
# Chỉ xoá pixel trắng/trong suốt liên thông với viền ảnh.
# Giữ nguyên chi tiết trắng BÊN TRONG logo (mũi tên trắng).
function Remove-WhiteBackground([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width; $h = $bmp.Height
    $keep = [bool[,]]::new($w, $h)   # true = pixel gốc được giữ

    # Ngưỡng: pixel có R>240 & G>240 & B>240 & A>200 là "nền trắng"
    function Is-White([System.Drawing.Color]$c) {
        return ($c.R -gt 240 -and $c.G -gt 240 -and $c.B -gt 240 -and $c.A -gt 200)
    }

    # Hàng đợi flood-fill từ biên; mỗi pixel mã hoá thành 1 số nguyên (y*w + x)
    $q = New-Object 'System.Collections.Generic.Queue[int]'
    $yBottom = $h - 1
    $xRight = $w - 1
    for ($x = 0; $x -lt $w; $x++) {
        if (Is-White($bmp.GetPixel($x, 0)))        { $q.Enqueue($x) }
        if (Is-White($bmp.GetPixel($x, $yBottom))) { $q.Enqueue($yBottom * $w + $x) }
    }
    for ($y = 0; $y -lt $h; $y++) {
        if (Is-White($bmp.GetPixel(0, $y)))        { $q.Enqueue($y * $w) }
        if (Is-White($bmp.GetPixel($xRight, $y)))  { $q.Enqueue($y * $w + $xRight) }
    }

    while ($q.Count -gt 0) {
        $idx = $q.Dequeue()
        $px = $idx % $w
        $py = [int][Math]::Floor($idx / $w)
        if ($keep[$px, $py]) { continue }
        if (-not (Is-White($bmp.GetPixel($px, $py)))) { continue }
        $keep[$px, $py] = $true
        if ($px -gt 0)             { $q.Enqueue($idx - 1) }
        if ($px -lt $xRight)       { $q.Enqueue($idx + 1) }
        if ($py -gt 0)             { $q.Enqueue($idx - $w) }
        if ($py -lt $yBottom)      { $q.Enqueue($idx + $w) }
    }

    # Tạo bitmap mới: pixel có $keep = true → alpha=0, giữ nguyên phần còn lại
    $out = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($x = 0; $x -lt $w; $x++) {
        for ($y = 0; $y -lt $h; $y++) {
            if ($keep[$x, $y]) {
                # Nền trắng → trong suốt
                $out.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
            } else {
                $out.SetPixel($x, $y, $bmp.GetPixel($x, $y))
            }
        }
    }
    return $out
}

# ─── Vẽ PNG vuông (scale-to-fit, căn giữa) ──────────────────
function New-SquarePng([System.Drawing.Bitmap]$src, [int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    $scale = [Math]::Min($size / $src.Width, $size / $src.Height)
    $w = [int][Math]::Round($src.Width * $scale)
    $h = [int][Math]::Round($src.Height * $scale)
    $x = [int][Math]::Floor(($size - $w) / 2)
    $y = [int][Math]::Floor(($size - $h) / 2)
    $g.DrawImage($src, $x, $y, $w, $h)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    # Dấu phẩy ngăn PowerShell unroll byte[] thành từng byte
    return ,$ms.ToArray()
}

# ─── Chính ────────────────────────────────────────────────────
if (-not (Test-Path $Source)) { throw "Không thấy file PNG nguồn: $Source" }

$raw = [System.Drawing.Image]::FromFile($Source)
$srcBmp = New-Object System.Drawing.Bitmap($raw)
$raw.Dispose()

Write-Host ("Nguồn: {0}x{1}" -f $srcBmp.Width, $srcBmp.Height)

# Xoá nền trắng flood-fill từ biên (giữ mũi tên trắng bên trong)
$clean = Remove-WhiteBackground $srcBmp
$srcBmp.Dispose()

$entries = @()
foreach ($size in $sizes) {
    $entries += ,@{ Size = $size; Bytes = (New-SquarePng $clean $size) }
}
$clean.Dispose()

# ─── Ghép .ico ────────────────────────────────────────────────
$count = $entries.Count
$offset = 6 + (16 * $count)

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)

$bw.Write([UInt16]0)          # reserved
$bw.Write([UInt16]1)          # type: icon
$bw.Write([UInt16]$count)     # số ảnh

foreach ($e in $entries) {
    $s = $e.Size
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([Byte]$dim)      # width
    $bw.Write([Byte]$dim)      # height
    $bw.Write([Byte]0)         # color count
    $bw.Write([Byte]0)         # reserved
    $bw.Write([UInt16]1)       # planes
    $bw.Write([UInt16]32)      # bit count
    $bw.Write([UInt32]$e.Bytes.Length)
    $bw.Write([UInt32]$offset)
    $offset += $e.Bytes.Length
}

foreach ($e in $entries) {
    $bw.Write($e.Bytes)
}

$bw.Flush()
$bytes = $ms.ToArray()
$bw.Dispose()
$ms.Dispose()

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$outFile = Join-Path $OutDir "app-icon.ico"
[System.IO.File]::WriteAllBytes($outFile, $bytes)

Write-Host ("OK: {0} ({1} bytes, {2} sizes)" -f $outFile, $bytes.Length, $count)
