# 清除 SaveFile.es3 中 gamepad.poc 的 stopped 记录（ModSteamGuard 误拦后重置用）
$saveFile = Join-Path $env:USERPROFILE "AppData\LocalLow\Questing Goose Studio\Probably Stolen\SaveFile.es3"
$raw = [System.IO.File]::ReadAllText($saveFile)

$bs = [char]92      # 反斜杠（文件里 JSON 字符串内的转义前缀）
$q  = [char]34      # 双引号

# 文件内实际文本: \"stopped\":[{...}]  （引号前有反斜杠转义）
$needle  = $bs + $q + 'stopped' + $bs + $q + ':['
$i = $raw.IndexOf($needle)
if ($i -lt 0) { Write-Host "未找到 stopped 键"; exit 0 }

$start = $i + $needle.Length
# 找到对应的中括号结束（记录内不包含 ] 字符）
$end = $raw.IndexOf(']', $start)
if ($end -lt 0) { Write-Host "未找到 stopped 数组结束"; exit 1 }

$inner = $raw.Substring($start, $end - $start)
if ($inner.Trim() -eq "") { Write-Host "stopped 已为空"; exit 0 }

Write-Host "清除内容: $($inner.Substring(0, [Math]::Min(120, $inner.Length)))..."
$new = $raw.Remove($start, $end - $start)
[System.IO.File]::WriteAllText($saveFile, $new, [System.Text.UTF8Encoding]::new($false))
Write-Host "已清除 stopped 记录并写回。"
