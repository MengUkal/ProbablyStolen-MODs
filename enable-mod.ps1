# ============================================================================
# enable-mod.ps1 — 自动启用一个已部署的本地 MOD（免游戏内 UI 操作）
# 原理：复刻 ModFingerprint 算法计算指纹 → 向 SaveFile.es3 注入
#       modOrder（启用列表）+ modTrust（含 developerMode=true 的信任记录）
# 用法： .\enable-mod.ps1 -ModId gamepad.poc
# 注意：游戏必须处于关闭状态！会自动备份 SaveFile.es3
# ============================================================================
param(
    [Parameter(Mandatory = $true)][string]$ModId,
    [string]$Folder,   # MOD 文件夹名；不填则用 ModId
    [string]$GameRoot = "D:\steamwork\steamapps\common\Probably Stolen",
    [string]$GameVersion = "0.46D"
)

$ErrorActionPreference = "Stop"
if (-not $Folder) { $Folder = $ModId }

$modsDir   = Join-Path $GameRoot "Mods"
$modFolder = Join-Path $modsDir $Folder
$saveFile  = Join-Path $env:USERPROFILE "AppData\LocalLow\Questing Goose Studio\Probably Stolen\SaveFile.es3"

# ---- 前置检查 ----
if (Get-Process -Name "Probably Stolen" -ErrorAction SilentlyContinue) {
    throw "游戏正在运行，请先退出游戏再执行本脚本。"
}
if (-not (Test-Path -LiteralPath $modFolder))        { throw "找不到 MOD 文件夹: $modFolder" }
if (-not (Test-Path -LiteralPath $saveFile))         { throw "找不到存档: $saveFile（先运行过游戏？）" }

$manifestPath = Join-Path $modFolder "manifest.xml"
if (-not (Test-Path -LiteralPath $manifestPath))     { throw "MOD 缺少 manifest.xml" }

# ---- 复刻 ModFingerprint.Compute ----
function Get-Sha256File($path) {
    (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
}
function Get-Sha256Bytes([byte[]]$bytes) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("x2") }) -join "" }
    finally { $sha.Dispose() }
}

[xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8
$assemblies = @($manifest.DocumentElement.SelectNodes("Assemblies/Assembly") | ForEach-Object { $_.InnerText.Trim().Replace('\', '/') })
if ($assemblies.Count -eq 0) { throw "manifest.xml 未列出任何 Assembly" }
$caps = @($manifest.DocumentElement.SelectNodes("Capabilities/Capability") | ForEach-Object { $_.InnerText.Trim().ToLowerInvariant() })

$manifestHash = Get-Sha256File $manifestPath
$sb = $manifestHash
$fileEntries = @()
foreach ($a in $assemblies) {
    $dllPath = Join-Path $modFolder ($a -replace '/', '\')
    if (-not (Test-Path -LiteralPath $dllPath)) { throw "manifest 声明的 DLL 不存在: $dllPath" }
    $h = Get-Sha256File $dllPath
    $sb = $sb + [char]10 + $a + "|" + $h
    $fileEntries += ("{0}|{1}" -f $a, $h)
}
$combined = Get-Sha256Bytes ([System.Text.Encoding]::UTF8.GetBytes($sb))

# ---- 构造信任记录（与 ModTrust.Approve 输出一致的紧凑 JSON）----
$now = (Get-Date).ToUniversalTime().ToString("o")
$kv = @()
function Add-Rec([string]$k, [string]$v) { $script:kv += '"' + $k + '":' + $v }

Add-Rec 'id'           ('"' + $ModId + '"')
Add-Rec 'source'       '"Local"'
Add-Rec 'workshopId'   '"0"'
Add-Rec 'manifestHash' ('"' + $manifestHash + '"')
$arrFiles = ($fileEntries | ForEach-Object { '"' + $_ + '"' }) -join ','
Add-Rec 'files'        ('[' + $arrFiles + ']')
Add-Rec 'combinedHash' ('"' + $combined + '"')
$arrCaps = ($caps | ForEach-Object { '"' + $_ + '"' }) -join ','
Add-Rec 'declared'     ('[' + $arrCaps + ']')
Add-Rec 'detected'     ('[' + $arrCaps + ']')
Add-Rec 'gameVersion'  ('"' + $GameVersion + '"')
Add-Rec 'approvedAt'   ('"' + $now + '"')
Add-Rec 'updatedAt'    ('"' + $now + '"')
Add-Rec 'developerMode' 'true'
Add-Rec 'scannerVersion' '3'
$recordInner = '{' + ($kv -join ',') + '}'

# ---- 注入 SaveFile.es3（文本手术 + 备份）----
$raw = [System.IO.File]::ReadAllText($saveFile)
$backup = "$saveFile.bak"
Copy-Item -LiteralPath $saveFile -Destination $backup -Force
Write-Host "已备份: $backup"

$changed = $false

# 1) modOrder 追加 ModId
$orderMatch = [regex]::Match($raw, '"modOrder":\{"__type":"(?<t>[^"]*)","value":\[(?<ids>[^\]]*)\]')
if (-not $orderMatch.Success) { throw "SaveFile.es3 中未找到 modOrder" }
$ids = $orderMatch.Groups['ids'].Value
if ($ids -notmatch [regex]::Escape('"' + $ModId + '"')) {
    $newIds = if ($ids.Trim() -eq "") { '"' + $ModId + '"' } else { $ids + ',"' + $ModId + '"' }
    $newOrder = '"modOrder":{"__type":"' + $orderMatch.Groups['t'].Value + '","value":[' + $newIds + ']'
    $raw = $raw.Remove($orderMatch.Index, $orderMatch.Length).Insert($orderMatch.Index, $newOrder)
    $changed = $true
    Write-Host "modOrder: 已追加 $ModId"
} else {
    Write-Host "modOrder: $ModId 已存在"
}

# 2) modTrust 追加记录
$escaped = $recordInner -replace '"', '\"'
$trustAnchor = '\"records\":['
$j = $raw.IndexOf($trustAnchor)
if ($j -lt 0) { throw "SaveFile.es3 中未找到 modTrust.records" }
if ($raw -notmatch [regex]::Escape('\"id\":\"' + $ModId + '\"')) {
    $insertAt = $j + $trustAnchor.Length
    $raw = $raw.Insert($insertAt, $escaped + ',')
    $changed = $true
    Write-Host "modTrust: 已注入 $ModId 的信任记录 (developerMode=true)"
} else {
    Write-Host "modTrust: $ModId 已有记录（如需强制刷新请先删除旧记录）"
}

if ($changed) {
    [System.IO.File]::WriteAllText($saveFile, $raw, [System.Text.UTF8Encoding]::new($false))
    Write-Host "已写入 SaveFile.es3" -ForegroundColor Green
} else {
    Write-Host "无需修改" -ForegroundColor Yellow
}
Write-Host "完成。下次启动游戏将自动加载 $ModId。"
