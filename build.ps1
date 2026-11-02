# ============================================================================
# Probably Stolen (深空当铺) MOD 一键构建 + 部署脚本
# 用法:
#   .\build.ps1                    构建全部 MOD (Release) 并部署到游戏 Mods 目录
#   .\build.ps1 -Mod GamepadPoc    只构建指定 MOD
#   .\build.ps1 -Launch            部署后启动游戏
#   .\build.ps1 -Clean             只清理 bin/obj
# ============================================================================
param(
    [switch]$Launch,
    [switch]$Clean,
    [string]$Mod = "all"   # ModTemplate | GamepadPoc | all
)

$ErrorActionPreference = "Stop"

$Mods = @("ModTemplate", "GamepadPoc", "ProfitLedger", "CustomerHud", "FastShutter", "ItemCodex")
if ($Mod -ne "all") { $Mods = @($Mod) }

$GameRoot   = "D:\steamwork\steamapps\common\Probably Stolen"
$ModsRoot   = Join-Path $GameRoot "Mods"
$ExePath    = Join-Path $GameRoot "Probably Stolen.exe"

if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "找不到游戏: $ExePath — 请修改脚本顶部的 GameRoot"
}

if ($Clean) {
    foreach ($m in $Mods) {
        $ProjectDir = Join-Path $PSScriptRoot $m
        dotnet clean $ProjectDir -v q | Out-Null
        Remove-Item -LiteralPath (Join-Path $ProjectDir "bin"), (Join-Path $ProjectDir "obj") -Recurse -Force -ErrorAction SilentlyContinue
    }
    Write-Host "已清理。" -ForegroundColor Yellow
    return
}

foreach ($m in $Mods) {
    $ProjectDir = Join-Path $PSScriptRoot $m
    if (-not (Test-Path -LiteralPath (Join-Path $ProjectDir "$m.csproj"))) { Write-Host "跳过 $m（无工程文件）"; continue }

    Write-Host "==> 构建 $m (Release)..." -ForegroundColor Cyan
    dotnet build $ProjectDir -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "构建失败: $m" }

    $Dll = Join-Path $ProjectDir "bin\Release\$m.dll"
    if (-not (Test-Path -LiteralPath $Dll)) { throw "未找到构建产物: $Dll" }

    $DestDir = Join-Path $ModsRoot $m
    New-Item -ItemType Directory -Force -Path $DestDir | Out-Null
    Copy-Item -LiteralPath $Dll -Destination $DestDir -Force
    Copy-Item -LiteralPath (Join-Path $ProjectDir "manifest.xml") -Destination $DestDir -Force

    foreach ($sub in @("Localization", "Textures", "Sounds", "LootTables", "Books")) {
        $src = Join-Path $ProjectDir $sub
        if (Test-Path -LiteralPath $src) {
            Copy-Item -LiteralPath $src -Destination $DestDir -Recurse -Force
        }
    }
    Write-Host "部署完成: $DestDir" -ForegroundColor Green
}

if ($Launch) {
    Write-Host "==> 启动游戏..." -ForegroundColor Cyan
    Start-Process -FilePath $ExePath -WorkingDirectory $GameRoot
}
