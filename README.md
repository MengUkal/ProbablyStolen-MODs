# Probably Stolen（深空当铺）MOD 开发环境

> 游戏路径：`D:\steamwork\steamapps\common\Probably Stolen`
> 本工作区：`D:\深空当铺MOD`

## 一、游戏解析结果

| 项目 | 结论 |
|---|---|
| 引擎 | Unity **2021.3.45f2 (LTS)**，**Mono** 脚本后端（非 IL2CPP） |
| API 兼容级别 | **netstandard2.1** |
| 开发商 | Questing Goose Studio |
| MOD 框架 | **游戏内置 ModLoader**（无 BepInEx / MelonLoader，无需安装任何注入器） |
| 补丁库 | 游戏自带 **0Harmony.dll**（Harmony 2.x），MOD 自动 PatchAll |
| 控制台 | 内置 Quantum Console（QFSW.QC） |
| 存档 | ES3（Easy Save 3） |
| 其他 | Steamworks.NET（创意工坊集成）、TextMeshPro、Unity Localization |

### 内置 MOD 系统要点（反编译确认）

- MOD 目录：`<游戏目录>\Mods\<Mod文件夹>\`，必须包含 **`manifest.xml`**
- 加载时机：主菜单场景 `ModLoader.Awake()`；只有玩家在游戏内 **MOD 菜单中启用**的 MOD 才会真正加载（记录在存档的 `modOrder`）
- 每个 DLL：先 `Harmony.PatchAll(程序集)`（**在 Init 之前**），再 `Init(manifest)` → `OnEnable()`；退出时 `OnDisable()` + UnpatchAll
- **信任机制**：首次启用会扫描 DLL 并弹出能力确认；`Mods` 里出现 manifest 未声明的 DLL 会被拒绝启用；开发者/官方信任列表由服务器下发（`ModTrustList.Fetch`）
- MOD ID 规则：仅 `a-zA-Z0-9_.-`，不以 `.` 开头，≤64 字符

## 二、已配置的环境

| 内容 | 位置 |
|---|---|
| 反编译参考源码 | `游戏反编译\Assembly-CSharp\`（全量 .cs + .csproj） |
| 模板 MOD 工程 | `ModTemplate\`（ModEntry.cs + manifest.xml + Localization/） |
| 一键构建部署脚本 | `build.ps1`（构建 → 复制到游戏 Mods 目录） |
| 已部署的示例 MOD | `<游戏>\Mods\ModTemplate\`（编译并部署完成，待游戏内启用） |
| .NET SDK | 9.0（已装，可直接编 netstandard2.1） |
| ilspycmd | 8.2（运行时需 `DOTNET_ROLL_FORWARD=LatestMajor`，或升级到 11.x） |

### 常用命令

```powershell
# 构建 + 部署所有 MOD（或 -Mod GamepadPoc 指定单个）
powershell -ExecutionPolicy Bypass -File build.ps1

# 自动启用某个 MOD（计算指纹 → 注入 SaveFile.es3，游戏须关闭）
powershell -ExecutionPolicy Bypass -File enable-mod.ps1 -ModId gamepad.poc -Folder GamepadPoc

# 游戏代码更新后重新生成 Transpiler 补丁目标清单
powershell -ExecutionPolicy Bypass -File gen-patch-targets.ps1

# 部署后顺便启动游戏 / 只清理
powershell -ExecutionPolicy Bypass -File build.ps1 -Launch
powershell -ExecutionPolicy Bypass -File build.ps1 -Clean
```

### 现有 MOD

| MOD | ID | 状态 |
|---|---|---|
| `ModTemplate\` | ModTemplate | 模板示例，已验证可加载 |
| `GamepadPoc\` | gamepad.poc | 手柄适配 Phase 0 POC：Transpiler 改写 100/101 方法，自检 335/335 PASS。注意此工程用 **net48** 目标（netstandard2.1 与 0Harmony 的 mscorlib 类型转发冲突）|
| `ProfitLedger\` | profit.ledger | 利润小账本：Tooltip 利润行（现估值-进价+利润率）+ 持有天数（随存档保存，夜间清理）+ 右上角今日经营统计栏（购/售/净）。已构建部署 |
| `CustomerHud\` | customer.hud | 顾客预览 HUD：右上角顾客面板（下一位顾客头像/名字/排队，位于统计栏下方 y=-112）+ 跳过顾客快捷键。与 profit.ledger 统计栏相互独立。字体走游戏 FontLoader（zh 中文字库）。已构建部署 |
| `FastShutter\` | fast.shutter | 快速卷帘门：Prefix 改写 StoreShutterButton.duration 私有字段（原版 2.75s），设置页可调。已构建部署 |
| `ItemCodex\` | item.codex | 物品图鉴：官方 CustomUI 框架，搜索/分类/已见过过滤/详情（默认 F9）。"已见过"随存档保存。已构建部署 |

**拆分说明**：五个玩法 MOD 互不依赖，可独立上传创意工坊。共享逻辑归属：AcqDays（账本）、DailyStats（HUD）、CodexSeen（图鉴）；ProfitLedger 与 ItemCodex 各自 Harmony 补丁 `PlayerStore.OnItemBought`，互不冲突。原 qol.pack 已解散并从游戏 Mods 目录移除（存档里的启用记录会被 ModLoader 安全忽略）。

## 五、创意工坊

| MOD | 物品 ID | 链接 |
|---|---|---|
| 利润小账本 Profit Ledger | 3832269768 | https://steamcommunity.com/sharedfiles/filedetails/?id=3832269768 |
| 顾客预览 HUD Customer HUD | 3832269928 | https://steamcommunity.com/sharedfiles/filedetails/?id=3832269928 |
| 快速卷帘门 Fast Shutter | 3832270015 | https://steamcommunity.com/sharedfiles/filedetails/?id=3832270015 |
| 物品图鉴 Item Codex | 3832270090 | https://steamcommunity.com/sharedfiles/filedetails/?id=3832270090 |

- 工坊素材（BBCode 描述 + AI 生成封面）在 `workshop-assets/<Mod>/`
- 更新已有物品：`WorkshopUploaderTool.exe --content <MOD目录> --desc-file <desc> --preview <preview> --id <物品ID> --changenote "..."`
- 封面由 GLM CogView 生成（`add-banner.ps1` 叠加标题栏与署名）
- 署名规范：所有 MOD 作者一律 **MengUkal**

## 三、MOD 文件夹结构（完整规范）

```
Mods/ModTemplate/
├── manifest.xml            ← 必须
├── ModTemplate.dll         ← manifest 里声明的程序集（放根目录或 Assemblies/ 子目录）
├── Assemblies/             ← 可选：DLL 也可放这里（路径写 Assemblies/ModTemplate.dll）
├── Localization/           ← 可选：*.csv，"key,value" 每行，# 注释，支持 zh/en 等按文件名匹配语言
├── Textures/               ← 可选：*.png/jpg，按文件名注册到 mod.Textures
│   ├── Items/              ←     物品像素图（Point 采样），注册为 mod.Sprites[文件名]
│   └── Paper/              ←     纸张/文件图，注册为 PaperSprites + Textures["Paper/文件名"]
├── Sounds/                 ← 可选：*.wav / *.ogg / *.mp3，mod.Sounds[文件名]
├── LootTables/             ← 可选：*.json 战利品表定义（ModLootFile）
└── Books/                  ← 可选：*.json 纸书定义（PaperBookRegistry）
```

**⚠ 任何未被 manifest 声明的 DLL（含依赖库、0Harmony 副本）出现在根目录或 Assemblies/ 都会被拒绝启用。** 所有游戏 DLL 引用务必 `Private=false`（模板 csproj 已配置）。

### manifest.xml 完整规范

```xml
<Manifest>
  <ID>ModTemplate</ID>            <!-- 必需；[a-zA-Z0-9_.-]，不以.开头，≤64字符 -->
  <Name>显示名</Name>
  <Author>作者</Author>
  <ModVersion>0.1.0</ModVersion>
  <Description>描述</Description>
  <GameVersions>                  <!-- 可选；仅 UI 展示匹配与否 -->
    <Version>1.0</Version>
  </GameVersions>
  <Prerequisites>                 <!-- 可选；依赖的其他 MOD ID -->
    <Mod>OtherModId</Mod>
  </Prerequisites>
  <Assemblies>                    <!-- 必需；相对路径 .dll，禁止 ".." -->
    <Assembly>ModTemplate.dll</Assembly>
  </Assemblies>
  <Capabilities/>                 <!-- 必需元素；不需要能力就写自闭合 -->
</Manifest>
```

## 四、能力（Capabilities）与信任机制

扫描器（ModScanner，基于 Cecil 静态分析）会检测 DLL 的实际能力用法，**声明的清单必须覆盖实际用法**，否则拒绝启用：

| 能力 ID | 触发条件 |
|---|---|
| `files` | `System.IO.File/Directory` 路径操作、ES3 自定义路径等 |
| `network` | UnityWebRequest、`System.Net` 等 |
| `process` | 打开链接 / 启动进程 |
| `dynamic-code` | `Assembly.Load`、`Emit`、`CodeDom` 等 |
| `native` | P/Invoke（DllImport）、指针、calli |
| `reflection-external` | 对游戏程序集之外的程序集做反射 |
| `steam` | Steamworks API 调用 |

- 只做 Harmony 补丁 + 游戏内 API 调用 + 读自己文件夹 → **`<Capabilities/>` 空即可**（模板即如此）
- 读自己 MOD 文件夹请优先用 `ModHelper.ReadModFile*` / `WriteModDataFile*`（不触发 files 能力，且有路径逃逸保护）
- 首次启用：游戏弹窗列出检测到的能力，玩家批准后按文件夹指纹记录；之后文件变动会提示"Changed"需重新批准

## 五、IMod 接口与生命周期

```csharp
public interface IMod
{
    void Init(ModManifest manifest);  // PatchAll 之后调用；配置项应在此定义
    void OnEnable();                  // 订阅 ModHook、注册调试按钮/游戏选项
    void OnDisable();                 // 必须成对注销！
}
```

**⚠ 陷阱**：`Init/OnEnable` 执行时本 MOD 尚未进入 `ModLoader.Mods` 列表，因此
`ModHelper.GetLocalized / GetModFolder` 等按 ID 查找的 API 必须在事件回调或 UI 回调中使用；
需要读自己文件夹内容请在 `ModHook.OnModAssetsLoaded` 之后。

### ModHook 事件（游戏生命周期总线，订阅即用，游戏会隔离异常）

| 事件 | 时机 |
|---|---|
| `OnGameLoadedInit/Early/Normal/Late` | 读档完成 |
| `OnNewGameStarted(scenarioId)` | 新游戏开始 |
| `OnDifficultyApplied(id)` | 难度应用 |
| `OnGenerateCustomerVeryEarly/Early/Normal/Late/VeryLate (StoreClientManager)` | 顾客生成各阶段 |
| `OnShutterOpened/Closed Early/Late` | 卷帘门开关（营业开始/结束） |
| `OnWakingUp / OnGoingSleep Early/Late` | 起床 / 入睡 |
| `OnLeavingStore / OnReturningStore Early/Late` | 离店 / 回店 |
| `OnHandlingNightlyServicesEarly/Late` | 夜间结算 |
| `OnPlaceInventorInventoryItemEarly/Late` | 发明家物品放置 |
| `OnCreateTooltip[Dev]Early/Late (RichTextBuilder, GameItem)` | 物品提示框 |
| `OnModAssetsLoaded(LoadedMod)` | 本 MOD 资源加载完毕 |
| `OnModConfigApplied(modId)` | MOD 配置应用 |
| `OnLootTablesLoaded` / `OnLootRolled(ctx)` | 战利品表 |
| `OnPaperOpened(bookId, PaperUI)` | 纸书打开 |
| `OnNetworkUpgradeUnlocked(id)` | 网络升级解锁 |
| `OnAfterhourArrive/Leave Early/Late`、`OnAfterhourEventStarted/Ended` | 深夜事件 |

### ModHelper 常用 API（静态类，游戏提供的官方 MOD 接口）

```csharp
// 经济 / 物品
ModHelper.AddCredit(100);
GameItem item = ModHelper.SpawnItem("itemId");
ModHelper.SetItemOwnerShip(item, true);
ModHelper.MoveItemToInventory(item);      // / MoveToFrontCounter / ToDisplayCase / ToTrash / ToAfterhours
ModHelper.SpawnFromLootTable("tableId");

// 本地化 / 音效
string s = ModHelper.GetLocalized(modId, "key", args...);   // mod 的 Localization/*.csv
ModHelper.PlaySound(modId, "soundKey");                     // mod 的 Sounds/ 文件

// UI 扩展
ModHelper.AddDebugFunction(modId, ModHelper.DEBUG_CATEGORY_MODDING, "名字", () => {...});
ModHelper.AddGameOptionToggle(modId, "gameplay", "optId", label, get, set, tooltip);
ModHelper.AddGameOptionSlider / Dropdown / KeyBind / AddGameOptionSection(...)

// 数据持久化（存档内，随玩家存档保存）
ModHelper.SetModData(modId, key, value) / GetModData / GetModDataInt|Float|Bool / RemoveModData ...

// 文件（仅限 MOD 自己的目录 / ModData 目录，路径逃逸保护）
ModHelper.ReadModFileText(modId, "sub/file.txt")
ModHelper.WriteModDataFileText(modId, "file.txt", text)
ModHelper.GetModDataFolder(modId)      // %USERPROFILE%\AppData\LocalLow\...\ModData\<modId>

// 战利品 / 网络升级 / 深夜事件
ModHelper.RegisterLootTable(modId, id, ("itemId", 1.0f), ...);
ModHelper.AddNetworkUpgrade(modId, id, cost, subtitle, title, desc, onUnlock, ...);
ModHelper.CreateAfterhourEffect(text, key, value, polarity);

// MOD 来源标记（新生成的顾客/物品标记归属）
ModHelper.SetItemSource(item, modId, modName);
```

### ModConfig（模组设置页）

```csharp
var cfg = ModHelper.GetConfig(modId);                      // 或 ModConfig.For(modId)
var b = cfg.Define("开关名", true, "General", () => "标签", () => "提示");
var i = cfg.Define("数量", 10, "General", () => "标签", 0, 100, () => "提示");
var e = cfg.DefineEnum("枚举", MyEnum.A, "General", () => "标签");
// 读取: b.Value   写入: b.Set(v)（Value 只读！Set 触发 OnChanged 事件）
// 持久化: %USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\ModConfigs\<modId>.json
```

## 六、开发工作流

> **署名规范**：所有 MOD 的 `manifest.xml` 作者署名一律用 **MengUkal**，不得使用其他署名。

1. **改代码** → `build.ps1`（自动构建并覆盖部署到游戏 Mods 目录）
2. **启动游戏** → 主菜单 MOD 菜单 → 找到 MOD → 启用 → 批准能力 → **按提示重启游戏**（`RestartRequired`）
3. **验证**：看日志 `[ModLoader]` / `[ModTemplate]` 前缀行

### 日志与调试

- 玩家日志：`%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\Player.log`
- MOD 配置：同目录 `ModConfigs\*.json`
- MOD 数据目录：同目录 `ModData\<modId>\`
- 加载失败时游戏会**自动弹出 Quantum Console**；控制台支持 `mod-scan` 等命令查看扫描详情
- MOD 加载顺序/启用状态存在存档（ES3）的 `modOrder` 键里

## 七、常见坑

1. **DLL 必须目标 netstandard2.1**；语言特性别用默认接口方法（Unity Mono 不支持）；模板已设 `LangVersion=9.0`
2. **游戏 DLL / 0Harmony 引用一律 `Private=false`**，禁止复制进 MOD 文件夹（未声明 DLL = 拒绝启用）
3. **程序集名不能与游戏任何程序集重名**（ModLoader 会按名字拒绝 "影子程序集"）
4. `ConfigEntry.Value` 只读，写入用 `Set()`
5. Harmony 补丁签名必须与目标方法参数完全一致（可选参数也要写）
6. `Init/OnEnable` 里不要调用按 ID 查找的 ModHelper API（见第五节陷阱）
7. `manifest.xml` 缺 `<Capabilities>` 元素（哪怕为空）或 `<Assemblies>` 为空都会解析失败
8. 构建脚本含中文需 UTF-8 **BOM**（Windows PowerShell 5.1 才能解析；本仓库 build.ps1 已带）
9. 反编译器 ilspycmd 8.2 要 .NET 6：设置环境变量 `DOTNET_ROLL_FORWARD=LatestMajor` 或 `dotnet tool update ilspycmd`
