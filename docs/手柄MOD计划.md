# 手柄适配 MOD 计划（GamepadSupport）

> ⛔ **项目已终止（2026-11-06）**：工作量超出收益，不再继续。
> 工坊物品 3831413572 已删除；GamepadPoc 工程已移除（git 历史可找回）。
> 本文保留作为输入虚拟化研究结论备查。


> 目标：让 Probably Stolen（深空当铺）可以**纯手柄**游玩，Windows + Steam Deck（Proton）通吃。
> 方案代号：`gamepad.support`

## 一、已验证的技术事实（决定方案）

| 事实 | 来源 | 影响 |
|---|---|---|
| 游戏只用**传统 Input Manager**（无 Input System 包） | Managed 目录无 `Unity.InputSystem.dll`；反编译源码全为 `UnityEngine.Input.*` 调用 | 手柄输入必须走自建轮询（XInput） |
| 输入读取全部汇聚在少数几个静态 API 上 | `Input.mousePosition`×82、`GetKeyDown`×66、`GetMouseButton`×43、`GetKey`×19、`anyKey`×3、`GetAxis`×1、`mouseScrollDelta`×2 | **在 `UnityEngine.Input` 静态方法上打 Harmony 补丁 = 一层虚拟化全游戏生效** |
| 游戏内物品拖拽/多选/机械面板等玩法都经 EventSystem RaycastAll + 自己构造 PointerEventData | `ItemContextHandler`、`ItemMultiSelectHandler`、`MachineryTabs`、`CounterMoveChild` 等 | 虚拟鼠标位置喂进 Input 后，这些玩法自动获得手柄支持 |
| 游戏自带完整**键位绑定系统** | `KeyBindHelper`：rotate_cw/ccw、flip_item、trash_item、quick_move_inventory/showcase/storage、sort_menu、call_next_customer、advance_dialogue、menu_back、open_options、toolbox_slot_N……存档键 `keybind_*` | 手柄按键→游戏动作有现成挂载点，还能在游戏设置里重映射 |
| `StoreUIManager` 存在 `Cursor.visible=false + lockState=Locked` 的模式切换 | StoreUIManager.cs:812-818 | 虚拟光标需与游戏的光标锁定逻辑互操作 |
| Steam Deck 经 Proton 运行，Steam Input 自动映射为 XInput | Proton 提供 xinput1_4 虚拟驱动 | **XInput P/Invoke 方案在 Deck 上零成本兼容** |

## 二、方案选型

| 方案 | 结论 |
|---|---|
| A. OS 级 SendInput 移动真实光标 | ❌ 否决：Deck/Proton 下不可靠、越权干扰桌面、无法读手柄 |
| B. Steam Input / Steamworks Action Sets | ❌ 否决：需要开发商在 Steamworks 后台配置动作清单，MOD 无法注入 |
| C. **进程内输入虚拟化**（选定） | ✅ XInput P/Invoke 轮询手柄 → Harmony 补丁 `UnityEngine.Input` 静态层 → 全游戏（UI+玩法）以虚拟鼠标/键盘驱动；MOD 只需声明 `native` 能力 |

## 三、架构设计（五层）

```
┌─ 5. 提示层  按键图形提示 / 操作说明（挂 CONTROL_HINTS 键位）
├─ 4. 映射层  手柄按钮 → KeyBindHelper 动作 + 虚拟键鼠事件（可重映射）
├─ 3. 光标层  虚拟光标覆盖图（Textures/cursor.png），摇杆驱动，接管真实光标可见性
├─ 2. 虚拟层  Harmony 补丁 UnityEngine.Input（mousePosition / GetMouseButton* /
│             GetKey* / mouseScrollDelta / GetAxis）——手柄未接入时原样透传
└─ 1. 源层    XInput P/Invoke 轮询（xinput1_4→1_3→9_1 依次探测），死区/曲线/震动
```

**默认按键映射（草案，全部可重映射）**

| 手柄 | 动作 |
|---|---|
| 左摇杆 | 移动光标（LT 按住=精确定位慢速） |
| 右摇杆 | 商店场景平移视角 / 列表滚动 |
| A | 确认 / 左键点击（按住=拖拽） |
| B | 取消 / 右键 / `menu_back` |
| X | `sort_menu` / 交互 |
| Y | `flip_item`（翻转物品） |
| LB/RB | `rotate_ccw`/`rotate_cw`；列表翻页 |
| RT | `advance_dialogue` |
| LT+Y | `trash_item` |
| 十字键 | `quick_move_inventory/showcase/storage` |
| Start | `open_options`；Back=`pin_mode` |
| 震动 | 成交/鉴定完成轻反馈（可选） |

## 四、已知难点与对策

1. **Unity 引擎方法（icall）能否被 Mono 上的 Harmony 补丁** → Phase 0 首个 POC 直接验证；若失败，退路是从测绘表逐个补丁游戏类读点（82+66 处虽多但机械，且都汇聚在少数 UI/控制器类）。
2. **拖拽体验**：A 按住=按下+跟随+抬起，需验证柜台↔柜台、柜台↔展示柜、多选拖动。
3. **鉴定/放大镜/纸编辑**等精细操作：LT 慢速模式 + 摇杆曲线可调。
4. **文本输入**（搜索框、存档命名）：Deck 用 Steamworks 浮动键盘（`ShowFloatingGamepadTextInput`，需声明 `steam` 能力）；Windows 端提示用鼠标。
5. **光标锁定模式**（812-818）与虚拟光标互操作：补丁该切换点或监听其状态。
6. **uGUI StandaloneInputModule 重复处理**风险：若出现双触发，改用自定义 BaseInputModule 替换。

## 五、分阶段执行计划

### Phase 0 — 测绘 + 可行性 POC ✅ 已完成（2026-11-01）
- [x] 输入面测绘：**217 个 Input 读点** → `docs/InputSurfaceMap.md`
- [x] POC 结论一：**Harmony 直接补丁 UnityEngine.Input（icall）在 Mono 上失败**（`InvalidProgramException`），该路线否决
- [x] 方案转向（B 方案落地）：**Transpiler IL 改写** —— 离线清单 101 个游戏方法，运行时把 `Input.X` 调用统一改写到代理类 `InputProxy`，实测 **100/101 成功**（唯一失败是 RaidScene 构造函数，可忽略）
- [x] uGUI 层发现：全部 UI 输入经 **BaseInput 虚方法**（托管代码）→ postfix 覆盖返回值即可，无需转写
- [x] XInput 探测：Mono 的 DllImport 解析器找不到 System32 的 xinput → **改用 Kernel32 LoadLibrary + GetProcAddress 手动绑定**，成功探测 `xinput1_4.dll`
- [x] 端到端自检：**SelfTest PASS（335/335 采样匹配）**——虚拟光标状态 → 代理 → 被改写方法读回，整条链路验证通过
- [x] 稳定性：游戏在 100 个方法被改写状态下正常进入主菜单，无崩溃
- 工程产物：`GamepadPoc\`（net48 + Microsoft.NETFramework.ReferenceAssemblies；netstandard2.1 编译目标与 0Harmony 的 mscorlib 类型转发冲突，不可用于含 Harmony 的 MOD）

### Phase 1 — MVP 可玩（2-3 天）
- [ ] `GamepadSupport` 正式工程（复用 POC 架构：InputProxy + Transpiler 清单 + BaseInput postfix + XInput LoadLibrary 层，声明 `<Capability>native</Capability>`）
- [ ] XInput 轮询层 + 死区/曲线 + 热插拔检测（POC 已有，迁移 + 调参 UI）
- [ ] 虚拟层：mousePosition / GetMouseButton(Down/Up) / mouseScrollDelta（POC 已验证）+ GetKeyDown/GetKeyUp 按键虚拟化（映射层）
- [ ] 虚拟光标覆盖图 + 手柄接入时隐藏系统光标；主菜单视觉验证 overlay（POC 期间被启动画面遮住，待主菜单窗口内确认）
- [ ] A=左键、B=右键/返回；游戏设置页注册开关与滑条（启用/光标速度/死区/震动）
- **验收**：仅用手柄完成——主菜单导航 → 读档 → 拿取物品 → 拖放柜台 → 打开笔记本 → 关闭

### Phase 2 — 全动作覆盖（2-3 天）
- [ ] KeyBindHelper 全部动作 → 手柄映射 + 设置页重映射 UI
- [ ] 按键图形提示（Textures/ 图标 + 操作说明挂 `CONTROL_HINTS`）
- [ ] 右摇杆视角平移/缩放；LT 精确模式；震动反馈
- **验收**：完整营业一日（接待→鉴定→讨价→收纳→打烊）全程手柄可玩

### Phase 3 — Steam Deck 打磨（1-2 天）
- [ ] Proton 实测；Steam Input 推荐布局文档；浮动键盘接入文本框
- [ ] 中英文本案补全；性能与耗电检查（轮询成本应 <0.1ms/帧）

## 六、项目结构

```
D:\深空当铺MOD\
├── GamepadSupport\            ← 新 MOD 工程（ID: gamepad.support）
│   ├── GamepadSupport.csproj  ← netstandard2.1，引用规则同模板
│   ├── Input\XInput.cs        ← P/Invoke 轮询层
│   ├── Input\VirtualMouse.cs  ← 虚拟光标状态机
│   ├── Patches\InputPatches.cs← UnityEngine.Input 虚拟化补丁
│   ├── ActionMap.cs           ← 按钮→KeyBindHelper 动作映射
│   ├── CursorOverlay.cs       ← 光标覆盖渲染
│   ├── GamepadOptions.cs      ← 游戏设置页集成
│   ├── manifest.xml           ← Capabilities: native（Phase 3 加 steam）
│   ├── Localization\zh.csv|en.csv
│   └── Textures\              ← 光标/按键图标
├── docs\InputSurfaceMap.md    ← Phase 0 产物
└── build.ps1                  ← 扩展为可部署多个 MOD
```

## 七、风险总表

| 风险 | 概率 | 状态 |
|---|---|---|
| Input 静态方法补丁在 Mono 失效 | ~~中~~ **已证实失效** | ✅ 已用 Transpiler 改写方案替代并验证（100/101） |
| 拖拽/精细操作手感差 | 中 | 待 Phase 1 实测；曲线/死区/慢速参数全开放 |
| Proton xinput 版本差异 | 低 | LoadLibrary 探测链已就位（Deck 的 Proton 提供 xinput1_4 虚拟驱动） |
| 与未来官方手柄支持冲突 | 低 | 所有补丁带开关，检测到原生手柄支持则自动让路 |
| Mono DllImport 解析器找不到系统 DLL | **已证实** | ✅ Kernel32 LoadLibrary 手动绑定解决 |
