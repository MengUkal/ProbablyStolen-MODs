using System;
using UnityEngine;

// ============================================================================
// 环带交易所 (stock.exchange) —— Probably Stolen 深空当铺
//   暗网解锁的股市系统：8 只空间站股票，行情与报纸事件（StoreEvent）联动。
//   - 解锁链：暗网升级 交易所接入(2500) → 量化终端(6000) → 做空执照(10000)
//   - 交易窗口：快捷键默认 F8（官方 CustomUI 框架）
//   - 每日收盘结算挂在起床/日切轮询上，独立 System.Random（不污染游戏 RNG）
//   - 全部状态走 ModHelper.SetModData（随存档 ES3 保存）
//   技术路线：T1 官方 API，零 Harmony 补丁。
// ============================================================================

public class StockExchangeEntry : IMod
{
    public const string ModId = "stock.exchange";

    // 暗网升级 ID（注册与查询使用同一字符串；NetworkUpgrade(id) 不做命名空间拼接）
    public const string UpAccess = "stock.exchange.access";
    public const string UpQuant = "stock.exchange.quant";
    public const string UpShort = "stock.exchange.short";

    public static StockExchangeEntry Instance { get; private set; }
    public ModManifest Manifest { get; private set; }

    internal KeyCode exchangeKey = KeyCode.F8;
    internal ConfigEntry<float> feePct;     // 手续费率 %（单边，最低 5）
    internal ConfigEntry<float> driftPct;   // 日漂移 %（长期趋势）
    internal ConfigEntry<float> volMult;    // 波动率倍率
    internal ConfigEntry<bool> cnColors;    // A股配色（红涨绿跌）
    internal ConfigEntry<bool> newsDaily;   // 每日收盘快报（报纸）

    public static string L(string key, params object[] args)
    {
        return ModHelper.GetLocalized(ModId, key, args);
    }

    internal float FeeRate() { return (feePct != null ? feePct.Value : 1f) / 100f; }
    internal float Drift() { return (driftPct != null ? driftPct.Value : 0.08f) / 100f; }
    internal float VolMult() { return volMult != null ? volMult.Value : 1f; }
    internal bool CnColors() { return cnColors != null && cnColors.Value; }
    internal bool NewsDailyOn() { return newsDaily != null && newsDaily.Value; }

    public void Init(ModManifest manifest)
    {
        Instance = this;
        Manifest = manifest;

        ModConfig cfg = ModHelper.GetConfig(ModId);
        feePct = cfg.Define(
            "FeePct", 1.0f, "Exchange",
            () => L("cfg_fee"), 0f, 5f, () => L("cfg_fee_tip"));
        driftPct = cfg.Define(
            "DriftPct", 0.08f, "Exchange",
            () => L("cfg_drift"), -1f, 1f, () => L("cfg_drift_tip"));
        volMult = cfg.Define(
            "VolMult", 1.0f, "Exchange",
            () => L("cfg_vol"), 0.2f, 3f, () => L("cfg_vol_tip"));
        cnColors = cfg.Define(
            "CnColors", false, "Exchange",
            () => L("cfg_colors"), () => L("cfg_colors_tip"));
        newsDaily = cfg.Define(
            "NewsDaily", true, "Exchange",
            () => L("cfg_news"), () => L("cfg_news_tip"));

        Debug.Log($"[OrbitExchange] Init: {manifest.Name} v{manifest.ModVersion}");
    }

    public void OnEnable()
    {
        // ---- 游戏设置页 ----
        ModHelper.AddGameOptionSection(ModId, "exchange", () => L("cfg_section"));
        ModHelper.AddGameOptionKeyBind(ModId, "exchange", "exchange_key",
            () => L("cfg_key"), () => exchangeKey, v => exchangeKey = v, () => L("cfg_key_tip"));
        ModHelper.AddGameOptionSlider(ModId, "exchange", "fee_pct",
            () => L("cfg_fee"), () => feePct.Value, v => feePct.Set(v),
            0f, 5f, false, () => L("cfg_fee_tip"), v => v.ToString("0.0") + "%");
        ModHelper.AddGameOptionSlider(ModId, "exchange", "drift_pct",
            () => L("cfg_drift"), () => driftPct.Value, v => driftPct.Set(v),
            -1f, 1f, false, () => L("cfg_drift_tip"), v => v.ToString("0.00") + "%");
        ModHelper.AddGameOptionSlider(ModId, "exchange", "vol_mult",
            () => L("cfg_vol"), () => volMult.Value, v => volMult.Set(v),
            0.2f, 3f, false, () => L("cfg_vol_tip"), v => "×" + v.ToString("0.0"));
        ModHelper.AddGameOptionToggle(ModId, "exchange", "cn_colors",
            () => L("cfg_colors"), () => cnColors.Value, v => cnColors.Set(v), () => L("cfg_colors_tip"));
        ModHelper.AddGameOptionToggle(ModId, "exchange", "news_daily",
            () => L("cfg_news"), () => newsDaily.Value, v => newsDaily.Set(v), () => L("cfg_news_tip"));

        // ---- 调试面板 ----
        ModHelper.AddDebugFunction(ModId, ModHelper.DEBUG_CATEGORY_MODDING,
            "Open Orbit Exchange", () => ExchangeUI.Toggle());
        ModHelper.AddDebugFunction(ModId, ModHelper.DEBUG_CATEGORY_MODDING,
            "Force News Digest", () => OrbitNews.ActivateDigestNow());

        // ---- 报纸市场版（P3-1）：注册收盘快报蓝图（public static 池，零反射）----
        OrbitNews.RegisterBlueprint();

        // 注意：暗网升级链【不在】OnEnable 注册！RegisterModUpgrade 内部会触碰 PlayerStore.Instance
        // （主菜单期是惰性单例，构造会 NRE → MOD 被标记 Init failed → LoadLocalization 被跳过 →
        //  本地化字典全空）。改到读档/新游戏回调里注册，见 EnsureUpgradesRegistered()。

        // ---- 生命周期挂钩 ----
        ModHook.OnGameLoadedNormal += OnGameLoaded;
        ModHook.OnNewGameStarted += OnNewGameStarted;
        ModHook.OnWakingUpEarly += OnWakingUpEarly;
        ModHook.OnWakingUpLate += OnWakingUpLate;
        ModHook.OnGoingSleepLate += OnGoingSleepLate;
        ModHook.OnNetworkUpgradeUnlocked += OnNetworkUpgradeUnlocked;

        ExchangeHost.Create();
        Debug.Log("[OrbitExchange] OnEnable: hooks + upgrade chain attached");
    }

    public void OnDisable()
    {
        ModHook.OnGameLoadedNormal -= OnGameLoaded;
        ModHook.OnNewGameStarted -= OnNewGameStarted;
        ModHook.OnWakingUpEarly -= OnWakingUpEarly;
        ModHook.OnWakingUpLate -= OnWakingUpLate;
        ModHook.OnGoingSleepLate -= OnGoingSleepLate;
        ModHook.OnNetworkUpgradeUnlocked -= OnNetworkUpgradeUnlocked;

        ModHelper.RemoveGameOptions(ModId);
        ModHelper.RemoveDebugFunctions(ModId);
        ModHelper.RemoveNetworkUpgrades(ModId);
        OrbitNews.UnregisterBlueprint();

        ExchangeHost.TearDown();
        ExchangeUI.OnTornDown();
        MarketData.OnTornDown();
        Debug.Log("[OrbitExchange] OnDisable: hooks detached");
    }

    // ------------------------------------------------------------ 游戏事件

    private void OnGameLoaded()
    {
        OrbitNews.ResetForGameLoad(); // 先重置快报轮询（防跨存档水位残留）
        MarketData.ReloadForGameLoad(GetDay()); // 先清内存态再从本存档 ModData 恢复（防跨存档串数据）
        EnsureUpgradesRegistered();
        Debug.Log("[OrbitExchange] OnGameLoaded: day=" + GetDay()
            + " activeEvents=" + ActiveEventCount());
        if (ExchangeUI.IsVisible)
        {
            ExchangeUI.RefreshAll();
        }
    }

    private void OnNewGameStarted(string scenarioId)
    {
        OrbitNews.ResetForGameLoad();
        MarketData.ResetForNewGame(GetDay());
        EnsureUpgradesRegistered();
        Debug.Log("[OrbitExchange] OnNewGameStarted: fresh account, day=" + GetDay());
    }

    // P0 时点验证日志：确认 dayCounter / 事件激活相对顺序
    private void OnWakingUpEarly()
    {
        Debug.Log("[OrbitExchange] WakeUpEarly day=" + GetDay() + " events=" + ActiveEventCount());
    }

    private void OnWakingUpLate()
    {
        bool settled = MarketData.EnsureAndSettle(GetDay());
        Debug.Log("[OrbitExchange] WakeUpLate day=" + GetDay()
            + " events=" + ActiveEventCount() + " settled=" + settled);
        if (settled && ExchangeUI.IsVisible)
        {
            ExchangeUI.RefreshAll();
        }
    }

    private void OnGoingSleepLate()
    {
        MarketData.Persist(); // 睡前兜底保存
        Debug.Log("[OrbitExchange] SleepLate: persisted (day=" + GetDay() + ")");
    }

    private void OnNetworkUpgradeUnlocked(string id)
    {
        if (id == UpAccess || id == UpQuant || id == UpShort)
        {
            Debug.Log("[OrbitExchange] upgrade unlocked: " + id + " tier=" + Broker.Tier());
            if (ExchangeUI.IsVisible)
            {
                ExchangeUI.RefreshAll();
            }
        }
    }

    // ------------------------------------------------------------ 暗网升级链

    /// 只能在读档/新游戏回调里调用：此时 PlayerStore 已存在（RegisterModUpgrade 需要它），
    /// 且 LoadLocalization 已执行完毕，subtitle/title/desc 才能正确解析。
    /// RegisterModUpgrade 按 id 幂等（RemoveAll+Add），重复调用安全。
    private void EnsureUpgradesRegistered()
    {
        try
        {
            // 三个升级共用同一 subtitle（=暗网联系人名"环带交易所"），才会归入同一个卡片分组
            ModHelper.AddNetworkUpgrade(ModId, UpAccess, 250,
                L("up_sub"),
                () => L("up_access_title"), () => L("up_access_desc"),
                () => OnUpgradeUnlocked(UpAccess),
                null, null, null, 0, null, null, true);
            ModHelper.AddNetworkUpgrade(ModId, UpQuant, 500,
                L("up_sub"),
                () => L("up_quant_title"), () => L("up_quant_desc"),
                () => OnUpgradeUnlocked(UpQuant),
                null, null, new[] { UpAccess });
            ModHelper.AddNetworkUpgrade(ModId, UpShort, 800,
                L("up_sub"),
                () => L("up_short_title"), () => L("up_short_desc"),
                () => OnUpgradeUnlocked(UpShort),
                null, null, new[] { UpQuant });
            Debug.Log("[OrbitExchange] 暗网升级链已注册 (250/500/800)");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[OrbitExchange] 升级链注册失败: " + e.Message);
        }
    }

    private void OnUpgradeUnlocked(string id)
    {
        Debug.Log("[OrbitExchange] purchased: " + id);
    }

    // ------------------------------------------------------------ 工具

    internal static int GetDay()
    {
        try
        {
            return StoreStation.Instance != null ? StoreStation.Instance.dayCounter : 0;
        }
        catch
        {
            return 0;
        }
    }

    internal static int ActiveEventCount()
    {
        try
        {
            var mgr = StoreStation.Instance != null ? StoreStation.Instance.storeEventManager : null;
            return mgr != null ? mgr.GetActiveEvents().Count : -1;
        }
        catch
        {
            return -1;
        }
    }
}
