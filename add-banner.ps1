# ============================================================================
# 在 AI 生成的封面原图 (raw.png) 上叠加标题栏与作者署名，输出 preview.png
# 用法: powershell -ExecutionPolicy Bypass -File add-banner.ps1
# 注意: 含中文，必须 UTF-8 with BOM
# ============================================================================
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = 'D:\深空当铺MOD'

$jobs = @(
  @{ mod = 'ProfitLedger'; title = '利润小账本';  sub = 'Profit Ledger' },
  @{ mod = 'CustomerHud';  title = '顾客预览 HUD'; sub = 'Customer HUD' },
  @{ mod = 'FastShutter';  title = '快速卷帘门';   sub = 'Fast Shutter' },
  @{ mod = 'ItemCodex';    title = '物品图鉴';     sub = 'Item Codex' }
)

foreach ($j in $jobs) {
  $dir = Join-Path $root (Join-Path $j.mod 'workshop')
  $raw = Join-Path $dir 'raw.png'
  if (-not (Test-Path $raw)) { Write-Host "SKIP $($j.mod)（无 raw.png）"; continue }

  $img = [System.Drawing.Image]::FromFile($raw)
  $canvas = New-Object System.Drawing.Bitmap($img.Width, $img.Height)
  $g = [System.Drawing.Graphics]::FromImage($canvas)
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.DrawImage($img, 0, 0, $img.Width, $img.Height)

  # ---- 底部渐变遮罩 + 标题栏 ----
  $bh = 130
  $gradRect = New-Object System.Drawing.Rectangle(0, ($canvas.Height - 220), $canvas.Width, 220)
  $gradBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($gradRect, [System.Drawing.Color]::FromArgb(0, 6, 8, 14), [System.Drawing.Color]::FromArgb(235, 6, 8, 14), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.FillRectangle($gradBrush, $gradRect)
  $gradBrush.Dispose()

  # 左侧强调色竖条
  $accent = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 123, 224, 123))
  $g.FillRectangle($accent, 0, ($canvas.Height - $bh), 14, $bh)
  $accent.Dispose()

  # 主标题（中文）+ 副标题（英文）+ 作者
  $titleFont = New-Object System.Drawing.Font('Microsoft YaHei UI', 40, [System.Drawing.FontStyle]::Bold)
  $subFont = New-Object System.Drawing.Font('Microsoft YaHei UI', 20)
  $authorFont = New-Object System.Drawing.Font('Microsoft YaHei UI', 16)
  $white = [System.Drawing.Brushes]::White
  $gray = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(215, 215, 225))
  $g.DrawString($j.title, $titleFont, $white, 40, ($canvas.Height - 112))
  $g.DrawString($j.sub, $subFont, $gray, 42, ($canvas.Height - 50))
  $authorText = 'by MengUkal · Probably Stolen MOD'
  $authorSize = $g.MeasureString($authorText, $authorFont)
  $g.DrawString($authorText, $authorFont, $gray, ($canvas.Width - $authorSize.Width - 36), ($canvas.Height - 46))

  $titleFont.Dispose(); $subFont.Dispose(); $authorFont.Dispose(); $gray.Dispose()
  $g.Dispose(); $img.Dispose()

  $outPng = Join-Path $dir 'preview.png'
  $canvas.Save($outPng, [System.Drawing.Imaging.ImageFormat]::Png)
  $canvas.Dispose()
  $kb = [math]::Round((Get-Item $outPng).Length / 1KB, 1)
  Write-Host ("OK  {0}  preview.png  {1} KB" -f $j.mod, $kb)
  if ($kb -gt 980) {
    # 超过 1MB 时改存 JPG
    $tmpBmp = [System.Drawing.Bitmap]::FromFile($outPng)
    $jpg = Join-Path $dir 'preview.jpg'
    $tmpBmp.Save($jpg, [System.Drawing.Imaging.ImageFormat]::Jpeg)
    $tmpBmp.Dispose()
    Remove-Item $outPng -Force
    Write-Host ("    PNG 超限，已改存 preview.jpg ({0} KB)" -f [math]::Round((Get-Item $jpg).Length/1KB,1))
  }
}
Write-Host '封面全部完成。'
