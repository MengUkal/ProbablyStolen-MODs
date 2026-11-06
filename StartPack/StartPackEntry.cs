using System;
using UnityEngine;

// ============================================================================
// 开局剧本包 (start.pack)
//
// 4 个新开局剧本（onStart 在 NewGameData 结算中触发，此时 invElement/playerCash/
// startingRent 均可用——与原版剧本使用同一套积木）：
//   1. 破产之子：继承债务（开局 -400 信用点，每周租金 +25）
//   2. 黑市弃徒：带一批来路不明的货开局（断手/等离子燃料/核废料）
//   3. 收藏家：满屋珍藏却囊中羞涩（三件鉴定行珍品 + 放大镜）
//   4. 赌徒：多带 500 信用点，外加两张彩票
//
// 4 个新初始天赋（效果在 ModHook.OnNewGameStarted 里实现——该事件在开局结算
// 末尾触发一次且 startingPerks 已就位，对官方剧本同样生效）：
//   · 启动资金(+2点)：开局信用点 +200
//   · 进货人脉(+2点)：开局仓库多三件常用物料
//   · 老伙计的工具箱(+1点)：开局多一个放大镜和标记笔
//   · 背债(-2点，负面)：开局信用点 -300
// ============================================================================

public class StartPackEntry : IMod
{
    public const string ModId = "start.pack";

    public static StartPackEntry Instance { get; private set; }
    public ModManifest Manifest { get; private set; }

    private bool hooksAttached;

    // 天赋 id -> 本 MOD 贴图名（Textures/Items/ 下，键为文件名不含扩展名）
    private static readonly System.Collections.Generic.Dictionary<string, string> PerkIconMap =
        new System.Collections.Generic.Dictionary<string, string>
        {
            { "sp_seed_cash", "icon_cash" },
            { "sp_seed_stock", "icon_stock" },
            { "sp_seed_tools", "icon_tools" },
            { "sp_seed_debt", "icon_debt" }
        };

    public static string L(string key, params object[] args)
    {
        return ModHelper.GetLocalized(ModId, key, args);
    }

    public void Init(ModManifest manifest)
    {
        Instance = this;
        Manifest = manifest;
        Debug.Log($"[StartPack] Init: {manifest.Name} v{manifest.ModVersion}");
    }

    public void OnEnable()
    {
        ModHook.OnNewGameStarted += OnNewGameStarted;
        ModHook.OnModAssetsLoaded += OnModAssetsLoaded;
        RegisterScenarios();
        RegisterPerks();
        InstallPerkIcons();
        hooksAttached = true;
        Debug.Log("[StartPack] OnEnable: 4 scenarios + 4 perks registered");
    }

    public void OnDisable()
    {
        if (hooksAttached)
        {
            ModHook.OnNewGameStarted -= OnNewGameStarted;
            ModHook.OnModAssetsLoaded -= OnModAssetsLoaded;
            ModHelper.RemoveStartingScenarios(ModId);
            ModHelper.RemoveStartingPerks(ModId);
            hooksAttached = false;
        }
        Debug.Log("[StartPack] OnDisable");
    }

    // ------------------------------------------------- 天赋图标注入
    // 特性网格控件(StartingPerkElement)只读 StartingPerkIconLoader.perkIcons 字典，
    // 而原版 Start() 每次会清空重填 vanilla 图标。Harmony postfix（见 Patches）保证
    // 每次清空后我们重新注入；OnModAssetsLoaded 兜底资产就绪后的首次注入。
    public static void InstallPerkIcons()
    {
        try
        {
            if (CustomUIManager.Instance == null)
            {
                return;
            }
            foreach (System.Collections.Generic.KeyValuePair<string, string> kv in PerkIconMap)
            {
                Sprite s = CustomUIManager.Instance.GetSprite(ModId + ":" + kv.Value, quiet: true);
                if (s != null)
                {
                    StartingPerkIconLoader.perkIcons[kv.Key] = s;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[StartPack] InstallPerkIcons failed: " + e.Message);
        }
    }

    private void OnModAssetsLoaded(LoadedMod mod)
    {
        // 仅在本 MOD 资产加载完成时注入一次（避免其他 MOD 资产事件重复执行）
        if (mod != null && mod.Manifest != null && mod.Manifest.ID == ModId)
        {
            InstallPerkIcons();
        }
    }

    // ---------------------------------------------------------------- 剧本
    // iconKey 采用 "MOD ID:贴图名" 格式，指向本 MOD Textures/ 目录（CustomUIManager.GetModSprite）
    private void RegisterScenarios()
    {
        // ---- 破产之子 ----
        ModHelper.AddStartingScenario(ModId, "sp_heir_debt",
            () => L("sc_heir_title"), () => L("sc_heir_body"),
            new Func<string>[] { () => L("sc_heir_gear1"), () => L("sc_heir_gear2"), () => L("sc_heir_gear3") },
            ScenarioHeirDebt,
            "start.pack:icon_debt", () => L("sc_heir_tag"), StartingScenario.TagTone.Danger);

        // ---- 黑市弃徒 ----
        ModHelper.AddStartingScenario(ModId, "sp_black_outcast",
            () => L("sc_outcast_title"), () => L("sc_outcast_body"),
            new Func<string>[] { () => L("sc_outcast_gear1"), () => L("sc_outcast_gear2"), () => L("sc_outcast_gear3") },
            ScenarioBlackOutcast,
            "start.pack:icon_stock", () => L("sc_outcast_tag"), StartingScenario.TagTone.Warning);

        // ---- 收藏家 ----
        ModHelper.AddStartingScenario(ModId, "sp_collector",
            () => L("sc_collector_title"), () => L("sc_collector_body"),
            new Func<string>[] { () => L("sc_collector_gear1"), () => L("sc_collector_gear2"), () => L("sc_collector_gear3") },
            ScenarioCollector,
            "start.pack:icon_cash", () => L("sc_collector_tag"), StartingScenario.TagTone.Positive);

        // ---- 赌徒 ----
        ModHelper.AddStartingScenario(ModId, "sp_gambler",
            () => L("sc_gambler_title"), () => L("sc_gambler_body"),
            new Func<string>[] { () => L("sc_gambler_gear1"), () => L("sc_gambler_gear2"), () => L("sc_gambler_gear3") },
            ScenarioGambler,
            "start.pack:icon_gamble", () => L("sc_gambler_tag"), StartingScenario.TagTone.Warning);
    }

    private static void ScenarioHeirDebt()
    {
        PlayerStore ps = PlayerStore.Instance;
        ps.playerCash -= 400;
        ps.startingRent += 25;
        ps.rentValue = ps.startingRent;
        ps.AddNightLog(L("nl_heir_debt"));
    }

    private static void ScenarioBlackOutcast()
    {
        PlayerStore ps = PlayerStore.Instance;
        ps.playerCash -= 100;
        Give(ps, "hand");
        Give(ps, "plasma_fuel");
        Give(ps, "nuclear_waste");
        Give(ps, "nuclear_waste");
        Give(ps, "plastic_bag");
        ps.AddNightLog(L("nl_outcast"));
    }

    private static void ScenarioCollector()
    {
        PlayerStore ps = PlayerStore.Instance;
        ps.playerCash -= 600;
        Give(ps, "hand");
        Give(ps, "ribwich");
        Give(ps, "plasma_fuel");
        Give(ps, "magnifier");
        ps.AddNightLog(L("nl_collector"));
    }

    private static void ScenarioGambler()
    {
        PlayerStore ps = PlayerStore.Instance;
        ps.playerCash += 500;
        ps.startingRent += 15;
        ps.rentValue = ps.startingRent;
        Give(ps, "scratcher");
        Give(ps, "lottery_ticket");
        ps.AddNightLog(L("nl_gambler"));
    }

    /// 把目录物品放进玩家仓库（与原版剧本同款积木；物品默认已归属玩家）。
    private static void Give(PlayerStore ps, string itemId)
    {
        try
        {
            GameItem item = DirectoryMaster.Item(itemId);
            if (item != null)
            {
                GraphUtils.EmporiumTryAdd(EmporiumEntry.Instance.invElement, item);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[StartPack] Give '" + itemId + "' failed: " + e.Message);
        }
    }

    // ---------------------------------------------------------------- 天赋
    private void RegisterPerks()
    {
        ModHelper.AddStartingPerk(ModId, "sp_seed_cash", 2, StartingPerk.StartingPerkType.POSITIVE,
            () => L("perk_cash_name"), () => L("perk_cash_desc"), "start.pack:icon_cash");
        ModHelper.AddStartingPerk(ModId, "sp_seed_stock", 2, StartingPerk.StartingPerkType.POSITIVE,
            () => L("perk_stock_name"), () => L("perk_stock_desc"), "start.pack:icon_stock");
        ModHelper.AddStartingPerk(ModId, "sp_seed_tools", 1, StartingPerk.StartingPerkType.POSITIVE,
            () => L("perk_tools_name"), () => L("perk_tools_desc"), "start.pack:icon_tools");
        ModHelper.AddStartingPerk(ModId, "sp_seed_debt", -2, StartingPerk.StartingPerkType.NEGATIVE,
            () => L("perk_debt_name"), () => L("perk_debt_desc"), "start.pack:icon_debt");
    }

    // ------------------------------------------------- 天赋效果（开局一次）
    private void OnNewGameStarted(string scenarioId)
    {
        try
        {
            PlayerStore ps = PlayerStore.Instance;
            if (ps == null)
            {
                return;
            }

            if (ModHelper.IsStartingPerkActive("sp_seed_cash"))
            {
                ps.playerCash += 200;
                ps.AddNightLog(L("nl_perk_cash"));
            }
            if (ModHelper.IsStartingPerkActive("sp_seed_stock"))
            {
                Give(ps, "scrap_metal");
                Give(ps, "common_electronic");
                Give(ps, "water_jug");
                ps.AddNightLog(L("nl_perk_stock"));
            }
            if (ModHelper.IsStartingPerkActive("sp_seed_tools"))
            {
                Give(ps, "magnifier");
                Give(ps, "marking_pen");
                ps.AddNightLog(L("nl_perk_tools"));
            }
            if (ModHelper.IsStartingPerkActive("sp_seed_debt"))
            {
                ps.playerCash -= 300;
                ps.AddNightLog(L("nl_perk_debt"));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[StartPack] OnNewGameStarted failed: " + e);
        }
    }
}
