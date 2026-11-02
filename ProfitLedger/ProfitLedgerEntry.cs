using System;
using UnityEngine;

// ============================================================================
// 利润小账本 (profit.ledger)
//   1. 物品 Tooltip 追加：利润（现估值-进价，含利润率）+ 持有天数
//      进价读游戏原生 BOUGHT_PRICE_TAG；持有天数随存档持久化，夜间清理孤儿条目
//   2. 右上角"今日经营统计"栏（购/售/净）——与顾客预览 HUD 相互独立
// ============================================================================

public class ProfitLedgerEntry : IMod
{
    public const string ModId = "profit.ledger";

    public static ProfitLedgerEntry Instance { get; private set; }
    public ModManifest Manifest { get; private set; }

    internal ConfigEntry<bool> profitLine;
    internal ConfigEntry<bool> heldDaysLine;
    internal ConfigEntry<bool> dailyStatsHud;

    private LedgerHud hud;
    private bool hooksAttached;

    public static string L(string key, params object[] args)
    {
        return ModHelper.GetLocalized(ModId, key, args);
    }

    public void Init(ModManifest manifest)
    {
        Instance = this;
        Manifest = manifest;

        ModConfig cfg = ModHelper.GetConfig(ModId);
        profitLine = cfg.Define(
            "ProfitLine", true, "Ledger",
            () => L("cfg_profit"), () => L("cfg_profit_tip"));
        heldDaysLine = cfg.Define(
            "HeldDaysLine", true, "Ledger",
            () => L("cfg_days"), () => L("cfg_days_tip"));
        dailyStatsHud = cfg.Define(
            "DailyStatsHud", true, "Ledger",
            () => L("cfg_daily_hud"), () => L("cfg_daily_hud_tip"));

        Debug.Log($"[ProfitLedger] Init: {manifest.Name} v{manifest.ModVersion}");
    }

    public void OnEnable()
    {
        ModHook.OnCreateTooltipLate += ProfitTooltip.OnTooltip;
        ModHook.OnGameLoadedNormal += OnGameLoaded;
        ModHook.OnNewGameStarted += OnNewGameStarted;
        ModHook.OnGoingSleepLate += OnGoingSleep;
        ModHook.OnShutterOpenedLate += DailyStats.CaptureBase;

        ModHelper.AddGameOptionSection(ModId, "ledger", () => L("cfg_section"));
        ModHelper.AddGameOptionToggle(ModId, "ledger", "ledger_profit_line",
            () => L("cfg_profit"), () => profitLine.Value, v => profitLine.Set(v), () => L("cfg_profit_tip"));
        ModHelper.AddGameOptionToggle(ModId, "ledger", "ledger_days_line",
            () => L("cfg_days"), () => heldDaysLine.Value, v => heldDaysLine.Set(v), () => L("cfg_days_tip"));
        ModHelper.AddGameOptionToggle(ModId, "ledger", "ledger_daily_hud",
            () => L("cfg_daily_hud"), () => dailyStatsHud.Value, v => dailyStatsHud.Set(v), () => L("cfg_daily_hud_tip"));

        hud = LedgerHud.Create();
        hooksAttached = true;
        Debug.Log("[ProfitLedger] OnEnable: hooks attached");
    }

    public void OnDisable()
    {
        if (hooksAttached)
        {
            ModHook.OnCreateTooltipLate -= ProfitTooltip.OnTooltip;
            ModHook.OnGameLoadedNormal -= OnGameLoaded;
            ModHook.OnNewGameStarted -= OnNewGameStarted;
            ModHook.OnGoingSleepLate -= OnGoingSleep;
            ModHook.OnShutterOpenedLate -= DailyStats.CaptureBase;
            ModHelper.RemoveGameOptions(ModId);

            if (hud != null)
            {
                UnityEngine.Object.Destroy(hud.gameObject);
                hud = null;
            }
            AcqDays.OnTornDown();
            hooksAttached = false;
        }
        Debug.Log("[ProfitLedger] OnDisable: hooks detached");
    }

    private void OnGameLoaded()
    {
        AcqDays.Restore();
        DailyStats.CaptureBase();
    }

    private void OnNewGameStarted(string scenarioId)
    {
        AcqDays.Reset();
        DailyStats.CaptureBase();
    }

    private void OnGoingSleep()
    {
        AcqDays.PruneAndPersist();
    }
}
