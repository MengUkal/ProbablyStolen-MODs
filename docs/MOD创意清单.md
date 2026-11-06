# 深空当铺（Probably Stolen）MOD 创意清单

> 基于 `游戏反编译\Assembly-CSharp\` 全量源码 + `ModHelper`/`ModHook` API 面梳理。
> 目的：为"接下来做什么 MOD"提供一份有依据、可排期的候选清单。

## 进展速览（2026-11-06 更新）

- ✅ 已完成上架：B1 利润小账本、B2 物品图鉴、B4 顾客预览 HUD、B7 快速卷帘门、C1-类开局剧本包（start.pack）、E6 环带交易所（stock.exchange）
- ⛔ 已放弃：A1 手柄适配（2026-11-06 终止，工坊物品已删除，POC 研究结论保留备查）
- 📦 发布链路已通：git 仓库（MengUkal/ProbablyStolen-MODs）+ WorkshopUploaderTool 四物品上架 + release 打包
- ⏭ 下一批推荐：C1 开局剧本包、C4 暗网升级树、C3 新难度（三者纯 API，可合为一个"开局增强包"条目）

## 〇、总原则

1. **能用官方 API 就不用 Harmony**：游戏自带 ModHelper 暴露了大量注册口（开局剧本/天赋/难度/暗网升级/鉴定题/深夜事件/战利品/纸书/设置页），纯 API 的 MOD 对游戏更新免疫、不需要能力声明、玩家信任弹窗最干净。
2. **受众优先**：Steam Deck / 手柄玩家（刚需、竞品少）> QoL（人人要）> 内容包（耐玩）> 规则改造（核心玩家）。
3. **每个 MOD 保持小而独立**，用 `Prerequisites` 组合，别做巨型缝合包。

## 一、可行性分级（技术路线）

| 等级 | 手段 | 稳定性 | 典型用途 |
|---|---|---|---|
| T1 | 纯官方 ModHelper / 资源目录（Textures/Sounds/LootTables/Books/Localization） | 游戏更新基本免疫 | 新剧本、新天赋、新难度、新事件、新物品、纸书 |
| T2 | ModHook 事件订阅 + 少量 Harmony 前后缀 | 较稳 | 顾客生成干预、Tooltip 附加信息、天灾/周期系统 |
| T3 | Harmony Prefix/Postfix 补丁游戏方法 | 游戏更新需复查 | 价格/经济逻辑、时间、图鉴数据挖掘 |
| T4 | Transpiler IL 改写 | 最脆（清单法已备 `gen-patch-targets.ps1`） | 输入虚拟化（手柄）、深层逻辑 |

## 二、点子清单

### A. 系统兼容类（刚需，受众最大）

| # | 点子 | 内容 | 路线 | 难度 | 备注 |
|---|---|---|---|---|---|
| A1 | **手柄支持 GamepadSupport** | 纯手柄游玩，Win + Deck 通吃 | T4（POC 已验证 Transpiler 改写 100/101） | 高 | **已在进行**，按 `docs\手柄MOD计划.md` 推进 Phase 1-3 |
| A2 | Steam Deck 优化包 | 虚拟键盘、触控板 gestures、耗电调优 | T4 + `steam` 能力（浮动键盘） | 中 | 可并入 A1 Phase 3 |
| A3 | 宽屏/超宽屏 UI 修正 | 21:9/32:9 布局修正 | T3 补丁 CanvasScaler/锚点 | 低-中 | 先实测是否有问题再立项 |

### B. QoL / 信息增强类（练手 + 高口碑）

| # | 点子 | 内容 | 路线 | 难度 |
|---|---|---|---|---|
| B1 | **利润小账本** | Tooltip 里追加"我的进价/预期利润/持有天数"；营业日结算页加当日利润曲线 | `OnCreateTooltipLate` + `OnHandlingNightlyServices` + ModData 存历史 | 低 |
| B2 | 物品图鉴/百科 | 全物品 ID、类别、来源掉落表、基准价；支持搜索（收录 MOD 来源并标注） | T3 反射 ItemDirectory 各目录 + 自绘 CustomUI | 中 |
| B3 | 鉴定助手 | 鉴定界面显示历史成交价参考区间（可配置开关，兼顾"作弊"争议 → 默认关） | Harmony AppraisalHelper + 配置项 | 中 |
| B4 | 顾客预览 | 显示下一位顾客类型图标（可开关）；"跳过废话顾客"快捷键 | `OnGenerateCustomer*` + KeyBind | 低 |
| B5 | 订单/请求看板 | StoreRequest 到期倒计时置顶提醒 | T2 hook | 低 |
| B6 | 一键收纳增强 | 展示柜按类别/利润自动摆放、过期请求物品快速下架 | 模拟 InventorySortHelper 逻辑 + Harmony | 中 |
| B7 | 快速睡觉/跳过过场 | 睡觉动画、打烊流程加速（倍率可调） | Harmony 时长参数补丁 | 低 |

### C. 玩法扩展类（官方 API 直加，最稳）

| # | 点子 | 内容 | 路线 | 难度 |
|---|---|---|---|---|
| C1 | **新开局剧本包** | "破产之子"（负债+高利贷起手）、"黑市弃徒"（初始违禁品+教会敌对）、"收藏家"（高价值低现金流）、"夜行者"（白天短/深夜长） | `AddStartingScenario` + `AddStartingPerk` | 低-中 |
| C2 | 新初始天赋 | 与剧本联动：销赃大师（赃物 heat 下降快）、火眼金睛（鉴定提示+1）、人脉（检查预警） | `AddStartingPerk` | 低 |
| C3 | **新难度** | "真实主义"（鉴定失误不可撤销、检查必罚）、"禅意"（无检查/无通缉/无催收） | `AddDifficulty`（configure DifficultyRules） | 低 |
| C4 | **暗网新升级树** | 暗网保险（被查损失返还50%）、洗白服务（赃物转合法）、情报贩子（提前1天预警检查）、匿名快递（违禁品直送） | `AddNetworkUpgrade` | 低 |
| C5 | 鉴定考核新题库 | 鉴定公会升阶考试加自定义物品鉴定题 | `AddAppraisalItem` | 低 |
| C6 | **深夜新地点** | 废弃空间站跳蚤市场、教会地下拍卖夜、"溜进海军仓库"（高风险高回报） | `AddAfterhourLocation` + `RegisterAfterhourAmbience` | 中 |
| C7 | 深夜新事件 | 海关大扫荡（N天检查强化）、黑市狂欢（违禁品溢价）、供应商失踪、太空瘟疫恐慌 | `AfterhourEvents.Register`（仿 VanillaAfterhourEvents）+ `OnAfterhourEventStarted` | 中 |
| C8 | 纸书/报纸包 | 自写小说集、攻略手册、玩家投稿故事集；新闻报纸增强 | `RegisterPaperBook` / PaperEditor 格式 JSON | 低 |
| C9 | 自定义游戏选项面板 | 把本清单所有 MOD 的开关统一进游戏设置页 | `AddGameOptionToggle/Slider/Dropdown/KeyBind` | 低 |

### D. 内容扩充类（耐玩，适合做成系列包）

| # | 点子 | 内容 | 路线 | 难度 |
|---|---|---|---|---|
| D1 | **新物品包 + 掉落接入** | 古董地球文物、外星生物标本、禁书、AI 核心等；写入太空遭遇/远征掉落表 | `OnModItemDirectoryInit` 注册 + Textures/Items + LootTables JSON | 中 |
| D2 | 新顾客类型 | 时间旅行者（倒卖过时/未来货）、AI 觉醒体（只收电子脑）、赏金猎人（卖通缉情报）、教会忏悔者（捐赃洗罪） | `OnGenerateCustomer*` 生成自定义 StoreClient + Localization 对话 | 中-高 |
| D3 | **太空遭遇扩展** | 幽灵商队、走私者求救信号（陷阱/宝藏）、太空鲸尸体（暴利+争议）、海军巡逻队盘查 | 仿 `SpaceExampleTrigger` 写新 Trigger | 中 |
| D4 | 远征 POI 扩展 | 新远征地点/事件（矿物带、失联殖民地） | `ExpeditionPoiList` | 中 |
| D5 | 农业/渔业/养殖扩展 | 新作物（辐射花）、观赏鱼种、变异宠物蛋 | PlantList / FishSpecies / AnimalSpecies 注册 | 中 |
| D6 | 枪械工坊扩展 | 新枪蓝图、枪模配件 | GunFactory / GunModDirectory | 中 |
| D7 | 家具装修包 | 主题套装（赛博霓虹/复古地球/教会风） | FurnitureItemDirectory + Textures | 中 |
| D8 | 音乐磁带包 | 自制电台磁带（Cassette 系统）+ 深夜氛围音 | Sounds/ + CassetteHelper + Ambience | 低 |

### E. 规则改造 / 挑战类（核心玩家，Harmony 为主）

| # | 点子 | 内容 | 路线 | 难度 |
|---|---|---|---|---|
| E1 | 硬核模式 | 单命；检查失败没收全部赃物；贷款逾期触发催收突袭（联动 RaidManager） | T3 补丁 + C3 难度入口 | 中-高 |
| E2 | 动态市场经济 | 卖多了价格下滑、囤货涨价；不同势力区间价格差 | PriceHelper 补丁 + ModData 记账 | 高 |
| E3 | **每日挑战 / 种子随机化** | 固定 seed 随机物价/顾客/掉落，社区比成绩 | RNG/TableMaster 补丁，seed 存档 | 中-高 |
| E4 | 天灾/危机系统 | 定期停电（机器停转）、员工罢工、舰队临检周 | T2 hook + 计时器 | 中 |
| E5 | 员工/雇佣增强 | 雇佣兵技能、忠诚度、自动柜台 | Hireling + Harmony | 高 |
| E6 | 股东/交易所玩法 | 发行股票融资、交易所期货 | ShareholderHelper/ExchangeBarter 深度改造 | 高 |

### F. 娱乐 / 梗类（小而有趣）

| # | 点子 | 内容 | 路线 | 难度 |
|---|---|---|---|---|
| F1 | 地球怀旧皮肤包 | 物品贴图替换为"地球古董"风 | Textures 覆盖（需确认覆盖机制） | 低-中 |
| F2 | 中文社区梗对话包 | 顾客台词玩梗改写 | Localization csv | 低 |
| F3 | 音效替换包 | 成交音/门铃/BGM 换装 | Sounds 覆盖 | 低 |
| F4 | 猫咪店长 | 角色/顾客贴图猫化 | 贴图资源路径确认后 | 中 |

## 三、推荐路线图

**第一批（1-2 周）**
1. 收尾 `GamepadSupport` Phase 1（MVP 可玩）→ 这是受众最广的刚需 MOD。
2. 并行做 1 个 T1 低难度 MOD 练手 + 走通创意工坊发布链路（`WorkshopUploaderTool` 已就绪）：推荐 **C1 新开局剧本包** 或 **C4 暗网新升级树**（前者展示度高，后者几乎零风险）。
3. **B1 利润小账本**：小而高频使用，口碑好做，T2 难度低。

**第二批**
4. **D1 新物品包**（搭配 D3 太空遭遇新 Trigger）→ 内容 MOD 的标杆作品。
5. **C7 深夜新事件 + C6 新地点** → 玩法纵深。

**第三批（视社区反馈）**
6. E3 每日挑战 / E2 动态经济（社区向玩法）。
7. B2 物品图鉴（等自研内容多了以后价值放大）。

## 四、风险与注意事项

1. **信任/能力声明**：只做 T1/T2 的 MOD `<Capabilities/>` 留空，玩家启用体验最好；涉及 XInput（A1）必须声明 `native`，会多一步弹窗确认——在描述里提前说明。
2. **游戏更新**：T3/T4 补丁可能失效；`gen-patch-targets.ps1` 重新生成清单后逐一复查；大版本后优先跑 GamepadPoc 自检。
3. **存档兼容**：ModData 随玩家存档保存，MOD 卸载后数据残留——提供"清除本 MOD 数据"选项（`ClearModData`）；避免把关键游戏状态只存 ModData。
4. **随机化/硬核类**要处理好"与成就系统"的关系（AchievementTracker），避免误导玩家被判作弊。
5. **发布节奏**：小 MOD 多发 > 单个大包慢发；每个 MOD 独立 manifest + 独立工坊条目，互为 Prerequisites。
6. **名称注意**：工坊条目避免使用"深空当铺"官方译名以外可能引起混淆的称呼；游戏英文名 Probably Stolen。
