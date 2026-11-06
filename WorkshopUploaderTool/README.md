# WorkshopUploaderTool — 创意工坊独立上传工具

以当前登录的 Steam 用户身份，为 Probably Stolen (AppID 4348910) 创建/更新创意工坊物品。
**不要在游戏进程内上传** — 游戏的 ModSteamGuard 会拦截并封禁 MOD（k_EResultAccessDenied / stopped）。

## 用法

```powershell
# 首次发布（创建）
.\WorkshopUploaderTool.exe --content "<MOD 文件夹>" --desc-file desc.txt --preview preview.png --visibility public

# 更新已有物品
.\WorkshopUploaderTool.exe --content "<MOD 文件夹>" --desc-file desc.txt --id <publishedFileId> --changenote "..."
```

## 前置条件（bin\Release 目录）

| 文件 | 来源 |
|---|---|
| `steam_appid.txt` | 内容 `4348910` |
| `steam_api64.dll` | **必须用较新 SDK 版本**（需含 `GetISteamGameSearch` 导出）。游戏自带的 10.24.16.75 太旧会让 SteamAPI.Init 崩溃；已验证可用：本机其他新游戏的分发副本（如 Slay the Spire 2 的 `data_sts2_windows_x86_64\steam_api64.dll`） |
| `Steamworks.NET.dll` | NuGet Steamworks.NET 2024.8（netstandard2.1，工具目标 net9.0） |
| `desc.txt` / `preview.png` | 工坊描述（支持 BBCode）/ 预览图（<1MB） |

## 已知坑

0. **--content 必须指向干净部署目录**（build.ps1 已自动暂存到 workshop-content\<Mod>\：manifest + DLL + 资源目录）。指向工程目录时根目录没有声明的 DLL，本地测试正常、**订阅用户加载失败且 MOD 不显示**——开局剧本包首发即犯此错。

1. **游戏进程内 MOD 上传会被 ModSteamGuard 硬阻断**并按文件哈希拉黑（清除方法：`clear-stopped.ps1` 或重建换哈希）。
2. 提交中途崩溃会**锁死该物品**（SubmitItemUpdate 永远 AccessDenied）——放弃该物品另建新的即可。
3. Steamworks.NET 2024.8 与游戏旧版 steam_api64.dll 二进制不兼容（缺入口点）。
4. 构建注意：Steamworks.NET 2024.8 仅支持 netstandard2.1+，工具用 net9.0。

## 已发布物品

- ~~`3831413572`~~ — Gamepad Support 测试版（2026-11-06 已删除：项目终止）
- ~~`3831412960`~~ — 废弃（首次崩溃锁死），建议网页端删除
- `3832269768` — 利润小账本 Profit Ledger（2026-11-02）
- `3832269928` — 顾客预览 HUD Customer HUD（2026-11-02）
- `3832270015` — 快速卷帘门 Fast Shutter（2026-11-02）
- `3832270090` — 物品图鉴 Item Codex（2026-11-02）
- `3833531537` — 环带交易所 Orbit Exchange（2026-11-04）

工坊素材（描述/预览图）统一放在 `workshop-assets/<Mod>/`，上传命令见 Git 历史。

## 新坑（2026-11-04 环带交易所首发实测）

5. **同一物品短时间内重复 SubmitItemUpdate 会 k_EResultFail**（约数分钟节流窗）。
   内容无变化的纯元数据提交也拦。处理：间隔 ≥10 分钟再提交；CreateItem 成功但
   首次提交失败不算锁死，用 `--id` 重试即可，物品不会废。
6. `k_EResultFail` 是通用拒绝码：先怀疑节流/网络，再怀疑内容——本机实测相同参数
   冷却后原样通过。
