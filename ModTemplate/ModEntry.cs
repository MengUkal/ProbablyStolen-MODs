using System;
using HarmonyLib;
using UnityEngine;

// ============================================================================
// Probably Stolen (深空当铺) MOD 模板入口
//
// 游戏内置 ModLoader 的加载流程 (反编译自 Assembly-CSharp.ModLoader):
//   1. 扫描 <游戏目录>\Mods\ 下的每个子文件夹, 读取 manifest.xml
//   2. 玩家在游戏"MOD"菜单中启用该 MOD 并批准其能力 (首次启用需要批准)
//   3. 对 manifest <Assemblies> 列出的每个 DLL:
//        harmony.PatchAll(你的程序集)   ← Harmony 补丁在 Init 之前自动应用!
//        mod.Init(manifest)             ← 你的入口类
//        mod.OnEnable()
//   4. 场景加载后: 加载 Textures / Localization / LootTables / Books / Sounds,
//      然后触发 ModHook.OnModAssetsLoaded
//   5. 退出游戏时: 调用 mod.OnDisable() 并 UnpatchAll
//
// 重要: 在 Init/OnEnable 执行时, 本 MOD 还没有进入 ModLoader.Mods 列表,
// 因此 ModHelper.GetLocalized / GetModFolder 等按 ID 查找的 API 要在
// 事件回调 (OnGameLoaded*/OnModAssetsLoaded) 或 UI 回调中调用, 不要在 Init 里直接调用。
// ============================================================================

public class ModTemplate : IMod
{
    public const string ModId = "ModTemplate";

    public static ModTemplate Instance { get; private set; }
    public ModManifest Manifest { get; private set; }

    // ---- 配置项 (持久化到 %USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\ModConfigs\ModTemplate.json)
    // 注意: ConfigEntry<T>.Value 是只读的, 写入用 Set(); 修改会触发 OnChanged 事件
    internal ConfigEntry<bool> logCashChanges;
    internal ConfigEntry<int> welcomeBonus;

    private bool hooksAttached;

    // ------------------------------------------------------------------ Init
    public void Init(ModManifest manifest)
    {
        Instance = this;
        Manifest = manifest;

        ModConfig config = ModHelper.GetConfig(ModId);

        // Define(key, 默认值, 分区, 标签, 工具提示)
        logCashChanges = config.Define(
            "LogCashChanges", true, "General",
            () => ModHelper.GetLocalized(ModId, "cfg_log_cash"),
            () => ModHelper.GetLocalized(ModId, "cfg_log_cash_tip"));

        // Define(key, 默认值, 分区, 标签, min, max, 工具提示)
        welcomeBonus = config.Define(
            "WelcomeBonus", 0, "General",
            () => ModHelper.GetLocalized(ModId, "cfg_bonus"),
            0, 5000,
            () => ModHelper.GetLocalized(ModId, "cfg_bonus_tip"));

        welcomeBonus.OnChanged += v => Debug.Log($"[ModTemplate] WelcomeBonus changed to {v}");

        Debug.Log($"[ModTemplate] Init: {manifest.Name} v{manifest.ModVersion} (ID={manifest.ID})");
    }

    // -------------------------------------------------------------- OnEnable
    public void OnEnable()
    {
        // ModHook 是游戏提供的生命周期事件总线, 订阅即可, 游戏会安全调用每个处理器
        ModHook.OnGameLoadedNormal += OnGameLoaded;
        ModHook.OnNewGameStarted += OnNewGameStarted;
        ModHook.OnLootRolled += OnLootRolled;

        // 调试面板按钮 (游戏内调试窗口 -> Modding 分类)
        ModHelper.AddDebugFunction(ModId, ModHelper.DEBUG_CATEGORY_MODDING,
            "Give 100 Credits", GiveTestCredits);

        // 游戏设置页新增开关 (设置 -> gameplay 分区)
        ModHelper.AddGameOptionToggle(ModId, "gameplay", "log_cash_verbose",
            () => ModHelper.GetLocalized(ModId, "cfg_log_cash"),
            () => logCashChanges.Value,
            v => logCashChanges.Set(v),
            () => ModHelper.GetLocalized(ModId, "cfg_log_cash_tip"));

        hooksAttached = true;
        Debug.Log("[ModTemplate] OnEnable: hooks attached");
    }

    // ------------------------------------------------------------- OnDisable
    public void OnDisable()
    {
        if (hooksAttached)
        {
            ModHook.OnGameLoadedNormal -= OnGameLoaded;
            ModHook.OnNewGameStarted -= OnNewGameStarted;
            ModHook.OnLootRolled -= OnLootRolled;
            ModHelper.RemoveDebugFunctions(ModId);
            ModHelper.RemoveGameOptions(ModId);
            hooksAttached = false;
        }
        Debug.Log("[ModTemplate] OnDisable: hooks detached");
    }

    // -------------------------------------------------------------- 事件处理
    private void OnGameLoaded()
    {
        // 此时可以安全地按 ID 查询本 MOD (ModLoader.Mods 已包含我们)
        string msg = ModHelper.GetLocalized(ModId, "msg_welcome", Manifest.Name);
        Debug.Log("[ModTemplate] " + msg);

        if (welcomeBonus.Value > 0 && PlayerStore.HasInstance)
        {
            PlayerStore.Instance.ModCash(welcomeBonus.Value, playSound: true);
            Debug.Log($"[ModTemplate] Paid welcome bonus: {welcomeBonus.Value}");
        }
    }

    private void OnNewGameStarted(string scenarioId)
    {
        Debug.Log($"[ModTemplate] New game started, scenario='{scenarioId}'");
    }

    private void OnLootRolled(LootRollContext ctx)
    {
        // ctx 里带本次掷点的上下文; 这里仅演示订阅
    }

    private void GiveTestCredits()
    {
        if (PlayerStore.HasInstance)
        {
            PlayerStore.Instance.ModCash(100, playSound: true);
        }
    }
}

// ============================================================================
// Harmony 补丁示例 —— 不需要自己 new Harmony()!
// ModLoader 在调用 Init 之前已对整个程序集 PatchAll(id="ModTemplate.ModTemplate")
// 方法签名必须与目标一致: public void ModCash(int value, bool playSound = true)
// ============================================================================
[HarmonyPatch(typeof(PlayerStore), "ModCash")]
internal static class PlayerStore_ModCash_Patch
{
    private static void Prefix(int value, bool playSound)
    {
        var mod = ModTemplate.Instance;
        if (mod == null || mod.logCashChanges == null || !mod.logCashChanges.Value)
        {
            return;
        }
        Debug.Log($"[ModTemplate] ModCash({value}, playSound={playSound})");
    }
}
