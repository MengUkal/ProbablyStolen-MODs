# 环带交易所（Orbit Exchange）—— 股市 MOD 策划案

| 项 | 内容 |
|---|---|
| MOD ID | `stock.exchange` |
| 显示名 | 环带交易所 Orbit Exchange |
| 作者 | MengUkal |
| 版本 | 策划案 v0.2（提案已批准，§12 决策已锁定） |
| **实施状态** | **P0+P1 已完成并部署（含暗网解锁链与事件联动）；P2 剩余做空/限价单；P3 剩余报纸市场版/PC 图标/手柄导航** |
| 日期 | 2026-11-04 |
| 对应创意清单 | E6 股东/交易所玩法 + E2 动态市场经济的轻量落地 |
| 技术路线 | 以 T1（官方 API）/ T2（ModHook）为主，仅 1 处可选 T3；**零能力声明** |

---

## 1. 一句话卖点

> 在当铺柜台后面偷偷炒股：读报纸、抓传闻、低买高卖，把黑心当铺做成对冲基金。

玩家白天收赃卖货，晚上看盘下单。**股市行情与空间站正在发生的事件（报纸新闻）真实联动**——种子短缺、矿船归港、全站停电，都会砸出或拉起对应的股票。核心乐趣不是赌，是**读报→提前布局→事件兑现**的信息差玩法。

---

## 2. 背景与动机

- 创意清单 E6（交易所）原本评估为 T3 高难度；本次摸底发现游戏官方 API 已覆盖 90% 需求，实际可按 **T1/T2 低风险路线**实现（见 §9）。
- 游戏自带完整的"世界在动"系统：`StoreEvent` 事件（含 `NegociationData` 物品价格修正）、报纸 UI（`NewsUIManager` 自动渲染激活事件）、**传闻机制**（`PickRumorEvent` 会把未来事件提前 2-3 天排队且 `IsKnown=true`——玩家能从报纸提前知道要出事）。这是天然的游戏素材，股市直接嫁接在上面，不另起炉灶。
- 博彩先例：游戏已有彩票（`LotteryHelper`，25/张、头奖 100 万、第 14 天解锁、每 7 天开奖）和刮刮乐。股市定位为它的互补品：**彩票=负期望买暴富梦，股市=正期望低频理财+信息玩法**，两者不冲突。
- 经济侧已有成熟通道：现金即 `PlayerStore.playerCash`（起步 600），`ModHelper.AddCredit(±n)` 可加可减；持久化走 `ModHelper.SetModData`（随存档保存）。

## 3. 设计目标

1. **好玩优先**：卖点是"读报布局"的信息差，不是数值赌博；无事件日行情窄幅震荡，抑制无脑梭哈。
2. **不破坏原版经济**：期望收益温和、有涨跌停/持仓上限/手续费三重闸门；股市亏损不影响租金与贷款主线。
3. **低侵入**：不打原版随机数、不改原版方法逻辑；游戏更新几乎不需要维护。
4. **独立可发布**：不依赖其他 MOD，可独立上传创意工坊；零能力声明，信任弹窗最干净。
5. **可调校**：所有数值参数进 ModConfig 设置页，方便自调和玩家自定义。

## 4. 世界观包装

空间站轨道上有一家老牌交易所"环带交易所"（Orbit Exchange），因为监管宽松（毕竟是个法外空间站），一直通过暗网向"有门路的人"开放终端接入。对当铺老板来说，这不过是把赃款换个地方洗——很合理。

- 股票 = 空间站上市的公司/机构，覆盖游戏既有事件涉及的行业（农贸、水务、能源、矿业、旅游、建筑、医药、艺术）。
- 新闻 = 复用原版报纸：原版事件发生时，相关股票异动；Phase 2 起股市自己也会在报纸上发"市场版"。

## 5. 核心玩法循环

```
早上起床(开盘) ──► 白天营业：偷空按 F8 看盘、下单（现金即时结算）
      ▲                      │
      │                      ▼
 睡觉(收盘结算) ◄── 读报纸/传闻：发现"3 天后矿船归港"
                             │
                             ▼
                    提前买入矿业股，等事件兑现日卖出
```

- **日频行情**（MVP）：日内价格恒定（= 昨日收盘），入睡时结算当日收盘并推进 K 线。白天看盘不焦虑，存档随时退。
- 市价单即时成交，T+0（当天买当天可卖；日频下不存在日内套利空间）。
- Phase 2 增加限价委托（挂单、次日按开盘价撮合）与做空。

## 6. 系统细则

### 6.1 首发标的（8 只）

| 代码 | 公司 | 板块 | 基准价 | 日波动 σ | 特征 |
|---|---|---|---|---|---|
| NFR | 营养果集团 Nutrifruit Foods | 农贸 | 40 | 2.5% | 稳健；种子短缺暴涨 |
| HYD | 环带水务 Orbital Waterworks | 公用 | 65 | 2.0% | 最稳，类债券 |
| PWR | 聚变能源 Fusion Dynamics | 能源 | 120 | 3.0% | 停电日暴涨 |
| MIN | 小行星矿业 Asteroid Mining Co. | 采矿 | 85 | 4.5% | 事件驱动型，传闻最爱 |
| CRZ | 星际邮轮 Starline Cruises | 旅游 | 55 | 4.0% | 邮轮靠港游客经济 |
| CON | 轨道建设 OrbitBuild | 建筑 | 30 | 3.5% | 低价股，工程事件多 |
| MED | 医学联合体 MedUnion | 医药 | 200 | 3.0% | 高价白马，短缺=订单 |
| ART | 星港艺术 NovaArt Guild | 奢侈品 | 15 | 7.0% | 妖股，暴涨暴跌 |

命名全部避让原版专有名词，走"通用行业公司"路线，降低与后续官方更新撞车的风险。

### 6.2 价格模型（每日收盘结算一次）

```
收盘价 = 开盘价 × (1 + drift + σ·gauss() + 事件冲击 + 板块联动 + 均值回归项)
```

| 参数 | 建议值 | 说明 |
|---|---|---|
| drift | +0.08% / 交易日 | 长期温和上行，鼓励参与 |
| σ（个股基础波动） | 见 6.1 表 | 高斯随机项 |
| 事件冲击 | ±6% ~ ±18% | 由激活的原版事件映射（6.3） |
| 板块联动 | 事件冲击 × 0.3 | 同板块其他股票沾光/遭殃 |
| 均值回归 | 偏离 30 日均线每 10% 回拉 1% | 防止单边漂移上天/归零 |
| 涨跌停 | ±25%（收盘 clamp） | 黑天鹅事件例外，可击穿 |
| 价格下限 | 1 | 不归零退市（MVP 简化）；退市机制放 Phase 4 |

### 6.3 事件联动（核心卖点）

读取 `StoreStation.Instance.storeEventManager` 的激活事件与未来事件（含传闻），按下表映射冲击：

| 原版事件 | 标的 | 冲击 | 玩法提示（报纸暗语） |
|---|---|---|---|
| seedShortage 种子被盗 | NFR | +15% | "果价要飞" |
| badNutrifruitHarvest 歉收 | NFR | -12% | |
| miningExpeditionReturn / miner_return_ice_asteroid | MIN | +15% | 传闻最常见，新手第一课 |
| stationWideBlackout 全站停电 | PWR +14%, ART -8% | | 应急电源概念 |
| powerShortage / 电站维护 | PWR +6% / -3% | | |
| water_treatment_breakdown | HYD +8% | | 水务抢修订单 |
| cruiseShipDocked 邮轮靠港 | CRZ +12%, ART +6% | | 游客消费 |
| construction_project / storeroom_excavated | CON +10% | | |
| NewTransitLine 新地铁线 | CON +8%, CRZ +4% | | |
| medicalSuppliesShortage 药品短缺 | MED +12% | | 短缺=订单 |
| ArtistShowcase / ArtContestHeld / CulturalCelebration | ART +10~14% | | |
| MaintenanceCrewStrike 维修罢工 | CON -6%, HYD -4% | | |

- 映射表数据驱动（代码内静态表，后续可外置 JSON），**原版事件改版/新增时只需补表**。
- 事件对当日还是未来 N 日生效：激活中事件→立即冲击；传闻（未来事件）→只在事件开始日兑现。传闻本身**不产生提前异动**（避免"看 K 线猜传闻"取代读报）。
- Phase 2：股市自有随机市场事件（"妖股躁动"随机一只 ±60%、"交易所故障日"全线 -25% 后两日修复），并注册为自定义 `StoreEvent` 让报纸自动刊登（需验证 `AfterhourEvents.Register`/事件注册路径对 MOD 的开放度，见 §11）。

### 6.4 交易规则

| 规则 | MVP | Phase 2 |
|---|---|---|
| 订单类型 | 市价单即时成交 | + 限价委托（隔夜撮合） |
| 交收 | 现金即时扣/入（`AddCredit`） | 不变 |
| 手续费 | 1%，最低 5 | 不变 |
| 持仓上限 | 按账户等级限单股持仓市值（§6.5） | 提升 |
| 做空 | 无 | 做空执照解锁：借券费 0.5%/日，保证金 150%，强平线 120% |
| 股息 | 无 | 可选：稳健股（HYD/NFR）季度的 1% 股息 |

防刷三闸门：**涨跌停**（封顶单日收益）+ **手续费**（抑制高频刷差价）+ **持仓上限**（封顶本金利用率）。核算：无事件日日均期望 ≈ +0.1%；抓到一次传闻兑现 ≈ +12~15%（单股上限内绝对收益约 600~750）。对比租金/卖货日均几百的现金流，股市是"锦上添花的第二曲线"，不是印钞机。

### 6.5 解锁与成长（暗网升级链，`AddNetworkUpgrade`）

| 阶段 | 升级项 | 建议定价 | 解锁内容 |
|---|---|---|---|
| 0 | — | — | 第 1~14 天：终端显示"交易所即将接入"（对齐彩票的 14 天节奏，前两周专心开店） |
| 1 | 交易所接入 Exchange Access | 250 | 股票账户 + 4 只稳健标的（HYD/NFR/CON/MED）+ 像素K线（按玩家反馈由 T2 下放为基础功能），单股持仓上限 5,000（价格按玩家反馈由 2,500 下调） |
| 2 | 量化终端 Quant Terminal | 500 | 全部 8 只 + 未来事件波动预警（仍不剧透具体事件），上限提升至 10,000 |
| 3 | 做空执照 Short License | 800 | 做空 + 限价委托（P2 开放），上限提升至 20,000（三档价格按玩家反馈定为 250/500/800） |

> 数值均为初稿，全部进 ModConfig；解锁节奏对标玩家第 2~3 周的现金流（日利润数百）。若实测过贵，P2 调参。

### 6.6 经济生态位

| | 彩票/刮刮乐 | 环带交易所 |
|---|---|---|
| 期望 | 负（买梦想） | 略正（理财+信息差） |
| 波动 | 极端（百万头奖 vs 归零） | 中等（±25% 涨跌停内） |
| 上限 | 无上限暴富 | 持仓上限封顶 |
| 技能 | 无 | 读报/事件记忆/仓位管理 |
| 解锁 | 第 14 天 | 暗网 2,500（约第 2~3 周） |

## 7. UI / UX

- **入口（MVP）**：快捷键默认 **F8**（游戏设置→按键绑定可改，`AddGameOptionKeyBind`；ItemCodex 已占 F9，不冲突）。营业中/夜间均可打开。
- **入口（Phase 3）**：店内电脑（`StorePC`）屏幕上加 "EXCHANGE" 图标按钮——需 Harmony/反射注入 `PCUIManager` 面板，作为可选增强。
- **窗口**：官方 CustomUI 框架（ItemCodex 已验证的 `CustomUIBuilder` 路线），布局：

```
┌─ 环带交易所 Orbit Exchange ──────────────────────┐
│ 账户现金 12,340    今日盈亏 +186 (+1.5%)    [持仓] │
├────────────┬────────────────────────────────────┤
│ 代码  现价  │  ▂▃▅▆▅▇MIN 小行星矿业   85 ▲+4.9%   │
│ HYD  65 ▲  │  事件标记: ⛏ 矿业回暖(报纸)          │
│ NFR  41 ▼  │  30日走势 ▁▂▃▃▄▅▆▇ (像素K线)        │
│ CON  30 ─  │  公司简介 / 我的持仓: 40股 @78       │
│ ...        │  [买入] [卖出]  数量: [10][-][+]     │
└────────────┴────────────────────────────────────┘
```

- K 线用运行时生成的像素纹理（`Texture2D` + Point 采样，30×16 网格），贴合游戏像素风；备选 RichText 方块字符方案。
- 涨跌用游戏 RichText 配色（红涨绿跌可配置——默认绿涨红跌，A股玩家可在设置里切换，`AddGameOptionToggle`）。
- **手柄/Deck 注意**：纯 UGUI 界面，GamepadPoc 的虚拟光标天然覆盖；P3 补 D-pad 焦点导航优化。本 MOD 不自行注册按键硬件，无 `native` 能力。

## 8. 存档与兼容性

- 全部状态走 `ModHelper.SetModData`（随玩家存档 ES3 保存），单 key JSON：
  - `stocks_state_v1`：8 股现价 + 最近 30 日收盘 + 持仓(数量/均价) + 已实现盈亏统计 + `lastSettledDay` + 账户等级
  - `stocks_seed_v1`：独立随机数种子（见 §9）
- **防重复结算**：每次收盘结算写 `lastSettledDay`；读档/重进不再补结算跳过的天数（行情断档，持仓不动——宁可断档不可重算，防止读档刷价格）。
- **卸载保护**：设置页提供"清仓并注销账户"按钮（按现价全部卖出→清空数据）；MOD 直接禁用后数据残留但无害，重新启用无缝续玩。收盘价快照在账，不依赖跨版本。
- 老存档启用：首次初始化按基准价生成历史（合成 30 日假 K 线），K 线不空窗。
- 不触碰原版成就系统（`AchievementTracker`）；盈亏统计只在 MOD 内部面板展示。

## 9. 技术方案

### 9.1 API 映射表（摸底结论）

| 功能 | 实现 | 等级 | 依据（反编译确认） |
|---|---|---|---|
| 加/扣钱 | `ModHelper.AddCredit(±n)`（内部 `PlayerStore.ModCash`） | T1 | ModHelper.cs / PlayerStore.cs:33 `playerCash` |
| 事件读取（激活+传闻） | `StoreStation.Instance.storeEventManager.activeEvents` / `GetActiveEventOnDay(day)` / `futurEvents`（公开字段） | T2 只读 | StoreEventManager.cs |
| 日期 | `StoreStation.Instance.dayCounter` | T2 只读 | 同上 |
| 日循环挂钩 | `ModHook.OnWakingUp`(开盘) / `OnGoingSleep` 或 `OnHandlingNightlyServices`(收盘结算) | T2 | README 事件表；具体时点 P0 日志验证 |
| UI | `CustomUIBuilder`（官方框架） | T1 | ItemCodex 先例 |
| 快捷键 | `AddGameOptionKeyBind` | T1 | ModHelper API |
| 解锁链 | `AddNetworkUpgrade` ×3 | T1 | ModHelper API（彩票即先例：`LotteryHelper.IsLotteryUnlocked`） |
| 持久化 | `ModHelper.SetModData/GetModData` | T1 | 随存档 ES3 保存 |
| 设置参数 | `ModConfig`（滑条/开关） | T1 | README API |
| 报纸联动（P2） | 注册自定义 `StoreEvent` 进蓝图列表（反射 append） | T3 可选 | StoreEventManager.normalEventBlueprints 为 static 字段 |
| PC 图标（P3） | 注入 `PCUIManager` 面板 | T3 可选 | PCUIManager.cs |

**结论：MVP 零 Harmony 补丁**，比 ItemCodex 还干净；仅 P2/P3 两个可选项涉及 T3，失效也不影响主功能。

### 9.2 随机数隔离（重要坑）

**绝不调用游戏 `RNG` 类**——它很可能是全局共享流，消耗会改变原版顾客生成/掉落等随机序列（隐性破坏原版体验）。市场随机使用独立的 `System.Random(seed)`，种子首次生成后存 `stocks_seed_v1`，保证同存档确定性（读档不重算）。

### 9.3 工程结构

```
StockExchange/
├── manifest.xml               ← ID stock.exchange，Capabilities 空自闭合
├── StockExchange.csproj       ← netstandard2.1，LangVersion 9，Private=false（模板抄）
├── StockExchangeEntry.cs      ← IMod：Init 注册配置/升级链，OnEnable 订阅 ModHook，OnDisable 成对注销
├── Features/
│   ├── MarketData.cs          ← 股票定义 + 价格状态 + 序列化
│   ├── PriceEngine.cs         ← 定价模型（System.Random 独立源）
│   ├── EventLink.cs           ← 事件→冲击映射表
│   ├── Portfolio.cs           ← 持仓/成交/盈亏
│   └── ExchangeHost.cs        ← 常驻 MonoBehaviour：快捷键轮询、结算调度
├── UI/
│   ├── ExchangeUI.cs          ← CustomUI 窗口（列表/详情/下单）
│   └── ChartTexture.cs        ← 像素 K 线生成
└── Localization/
    ├── zh.csv                 ← 中文名/公司简介/提示
    └── en.csv
```

构建与发布沿用现有链路：`build.ps1 -Mod StockExchange` → `enable-mod.ps1` → 验证 `[ModLoader]` 日志 → `WorkshopUploaderTool` 发布（署名 MengUkal）。

## 10. 分期计划

| 阶段 | 内容 | 验收标准 | 估时 |
|---|---|---|---|
| P0 骨架 | 工程+manifest+空 UI（快捷键打开）+ModHook 时点日志验证 | 游戏内窗口可开合，日志确认 OnWakingUp/Sleep 时序 | 0.5 天 |
| P1 MVP | 8 股+定价引擎+列表/详情/K线+市价买卖+持仓盈亏+存档+防重结算 | 连续 10 游戏日无报错；存读档一致；价格曲线无漂移爆炸；现金收支与成交记录对账无误 | 2-3 天 |
| P2 联动 | 事件冲击+传闻布局玩法+暗网解锁链+做空+限价单 | 原版事件兑现日价格异动可复现；做空强平逻辑正确 | 2-3 天 |
| P3 打磨 | 报纸市场版（T3）+PC 图标（T3）+股息+手柄导航+盈亏统计面板 | 报纸出现市场版块；电脑可点图标 | 1-2 天 |
| 发布 | ModConfig 全参数、双语、封面（CogView+add-banner）、工坊上传 | — | 0.5 天 |

## 11. 风险与对策

| # | 风险 | 对策 |
|---|---|---|
| 1 | `ModHook` 结算时点语义与预期不符（睡眠/夜服顺序） | P0 专门日志验证，必要时监听 `dayCounter` 变化兜底 |
| 2 | 游戏事件标识符改版 | 映射表数据驱动集中管理；`gen-patch-targets.ps1` 复查（仅 P2/P3 的 T3 点） |
| 3 | 数值失衡（太赚毁经济/太亏没人用） | 全参数 ModConfig；发布前自测 30 天对照局（开/关 MOD 各一） |
| 4 | CustomUI 渲染 K 线受限 | 备选 RichText 方块字符方案，一天内可切换 |
| 5 | 玩家中途卸载导致持仓蒸发争议 | 设置页"清仓注销"入口 + 工坊描述明示；数据残留可无损重开 |
| 6 | 与手柄 MOD 按键冲突 | 本 MOD 不注册物理按键，仅游戏内 KeyBind 选项；Deck 上建议用虚拟光标点窗口 |
| 7 | 信任弹窗 | 零能力声明（无 files/network/native），首次启用零确认项 |

## 12. 设计决策（已拍板，2026-11-04）

| # | 决策 | 结论 |
|---|---|---|
| 1 | 账户制 vs 实体股票凭证 | **账户制**（实体凭证留作 Phase 4 彩蛋） |
| 2 | 解锁方式 | **暗网升级链**（§6.5 三级：2,500 / 6,000 / 10,000） |
| 3 | 做空 | **保留在 Phase 2** |
| 4 | 命名 | **`stock.exchange` / 环带交易所 Orbit Exchange** |
| 5 | 涨跌配色 | **默认绿涨红跌**，设置提供 A 股配色开关 |

---

*附：摸底依据均来自 `游戏反编译\Assembly-CSharp\`（StoreEventManager / PlayerStore / ModHelper / LotteryHelper / PCUIManager / NewsUIManager）与本仓库 `README.md` 的 ModLoader 规范；实现前如游戏更新，先重跑 `gen-patch-targets.ps1` 核对 §9.1 映射表。*
