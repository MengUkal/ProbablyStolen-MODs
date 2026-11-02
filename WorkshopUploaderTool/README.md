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

1. **游戏进程内 MOD 上传会被 ModSteamGuard 硬阻断**并按文件哈希拉黑（清除方法：`clear-stopped.ps1` 或重建换哈希）。
2. 提交中途崩溃会**锁死该物品**（SubmitItemUpdate 永远 AccessDenied）——放弃该物品另建新的即可。
3. Steamworks.NET 2024.8 与游戏旧版 steam_api64.dll 二进制不兼容（缺入口点）。
4. 构建注意：Steamworks.NET 2024.8 仅支持 netstandard2.1+，工具用 net9.0。

## 已发布物品

- `3831413572` — Gamepad Support (Steam Deck 测试版) 0.0.3
- ~~`3831412960`~~ — 废弃（首次崩溃锁死），建议网页端删除
