# ============================================================================
# 生成 StartPack 的像素图标（16x16 图案矩阵 -> 64x64 PNG）到 Textures/
# 用法: powershell -ExecutionPolicy Bypass -File add-icons.ps1
# 含中文注释，必须 UTF-8 with BOM
# ============================================================================
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$outDir = 'D:\深空当铺MOD\StartPack\Textures\Items'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# 色板：. 透明 / k 描边 / g 金色主 / G 金高光 / b 木箱主 / B 木箱深 / w 绑带米 / c 玻璃青 / h 手柄棕 / R 纸红 / W 纸白 / r 赤点
# PS 哈希字面量键不区分大小写（g/G、b/B、W/w、R/r 会冲突），改用区分大小写的泛型 Dictionary
$palette = New-Object 'System.Collections.Generic.Dictionary[string,System.Drawing.Color]'
$palette['.'] = [System.Drawing.Color]::Empty
$palette['k'] = [System.Drawing.Color]::FromArgb(255, 42, 32, 24)
$palette['g'] = [System.Drawing.Color]::FromArgb(255, 232, 184, 74)
$palette['G'] = [System.Drawing.Color]::FromArgb(255, 255, 224, 138)
$palette['b'] = [System.Drawing.Color]::FromArgb(255, 138, 90, 50)
$palette['B'] = [System.Drawing.Color]::FromArgb(255, 94, 61, 32)
$palette['w'] = [System.Drawing.Color]::FromArgb(255, 217, 201, 160)
$palette['c'] = [System.Drawing.Color]::FromArgb(255, 154, 219, 232)
$palette['h'] = [System.Drawing.Color]::FromArgb(255, 120, 78, 46)
$palette['R'] = [System.Drawing.Color]::FromArgb(255, 201, 74, 58)
$palette['W'] = [System.Drawing.Color]::FromArgb(255, 242, 234, 216)
$palette['r'] = [System.Drawing.Color]::FromArgb(255, 170, 52, 40)

# 图案（16x16，每个字符一格）
$icons = @{
  # 金币堆（启动资金 / 收藏家）
  'icon_cash' = @(
    '................'
    '................'
    '................'
    '....kkkkkkkk....'
    '...kGGGGGGGGk...'
    '...kgggggggGgk..'
    '...kgkkkkkgkgk..'
    '....kkkkkkkk....'
    '...kkkkkkkkkk...'
    '..kGGGGGGGGGGk..'
    '..kgggggggggGgk.'
    '..kgkkkkkkggGgk.'
    '..kgkkkkkkggGgk.'
    '..kgggggggggGgk.'
    '..kkkkkkkkkkkk..'
    '................'
  )
  # 物料木箱（进货人脉 / 黑市弃徒）
  'icon_stock' = @(
    '................'
    '................'
    '................'
    '................'
    '..kkkkkkkkkkkk..'
    '..kbbbbwwbbbbk..'
    '..kbwwwbbwwwbk..'
    '..kbwwwbbwwwbk..'
    '..kbbbbwwbbbbk..'
    '..kbwwwbbwwwbk..'
    '..kbwwwbbwwwbk..'
    '..kbbbbwwbbbbk..'
    '..kkkkkkkkkkkk..'
    '................'
    '................'
    '................'
  )
  # 放大镜（老伙计的工具箱）
  'icon_tools' = @(
    '................'
    '....kkkkkk......'
    '...kccccccck....'
    '..kcWWccccccck..'
    '..kcWccccccck...'
    '..kccccccck.....'
    '..kccccccck.....'
    '...kcccccck.....'
    '....kkkkkk......'
    '.......kkkk.....'
    '........kkhh....'
    '.........khhk...'
    '..........khhk..'
    '...........khhk.'
    '............kk..'
    '................'
  )
  # 债券（背债 / 破产之子）
  'icon_debt' = @(
    '................'
    '..kkkkkkkkkkkk..'
    '..kWWWWWWWWWWk..'
    '..kWRRRRRRRRWk..'
    '..kWRWWWWWWRWk..'
    '..kWRWRRRRRWRk..'
    '..kWRWRWWWWRWk..'
    '..kWRWRRRRRWRk..'
    '..kWRWWWWWWRWk..'
    '..kWRRRRRRRRWk..'
    '..kWWWWWWWWWWk..'
    '..kWrrWWWWrrWk..'
    '..kWWrrWWrrWWk..'
    '..kWWWrrrrWWWk..'
    '..kkkkkkkkkkkk..'
    '................'
  )
  # 骰子（赌徒）
  'icon_gamble' = @(
    '................'
    '................'
    '..kkkkkkkkkkk...'
    '..kWWWWWWWWWk...'
    '..kWrWWWWWkWWk..'
    '..kWWrWWWWWWWk..'
    '..kWWWWrWWWWk...'
    '..kWWrWWWWWWWk..'
    '..kWrWWWWWkWWk..'
    '..kWWWWWWWWWk...'
    '..kkkkkkkkkkk...'
    '................'
    '................'
    '................'
    '................'
    '................'
  )
}

$scale = 4  # 16 格 x4 = 64px
foreach ($name in $icons.Keys) {
  $rows = $icons[$name]
  $size = 16 * $scale
  $bmp = New-Object System.Drawing.Bitmap($size, $size)
  for ($row = 0; $row -lt 16; $row++) {
    $line = $rows[$row]
    for ($col = 0; $col -lt 16; $col++) {
      $ch = $line.Substring($col, 1)
      if (-not $palette.ContainsKey($ch) -or $palette[$ch] -eq $null) { continue }
      $brush = New-Object System.Drawing.SolidBrush($palette[$ch])
      $g = [System.Drawing.Graphics]::FromImage($bmp)
      $g.FillRectangle($brush, $col * $scale, $row * $scale, $scale, $scale)
      $g.Dispose()
      $brush.Dispose()
    }
  }
  $out = Join-Path $outDir ($name + '.png')
  $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Write-Host ("OK  {0}" -f $out)
}
Write-Host '图标生成完毕。'
