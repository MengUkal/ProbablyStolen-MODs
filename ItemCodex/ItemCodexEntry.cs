using System;
using UnityEngine;

// ============================================================================
// 物品图鉴 (item.codex)
//   - 全物品浏览窗口（基于游戏官方 CustomUI 框架）
//   - 搜索（防抖）/ 分类下拉 / "已见过"范围过滤（随存档保存）
//   - 快捷键（默认 F9，设置页可改）+ 调试面板按钮
// ============================================================================

public class ItemCodexEntry : IMod
{
    public const string ModId = "item.codex";

    public static ItemCodexEntry Instance { get; private set; }
    public ModManifest Manifest { get; private set; }

    internal KeyCode codexKey = KeyCode.F9;

    private CodexHost host;
    private bool hooksAttached;

    public static string L(string key, params object[] args)
    {
        return ModHelper.GetLocalized(ModId, key, args);
    }

    public void Init(ModManifest manifest)
    {
        Instance = this;
        Manifest = manifest;
        Debug.Log($"[ItemCodex] Init: {manifest.Name} v{manifest.ModVersion}");
    }

    public void OnEnable()
    {
        ModHook.OnGameLoadedNormal += OnGameLoaded;
        ModHook.OnNewGameStarted += OnNewGameStarted;
        ModHook.OnGoingSleepLate += OnGoingSleep;

        ModHelper.AddGameOptionSection(ModId, "codex", () => L("cfg_section"));
        ModHelper.AddGameOptionKeyBind(ModId, "codex", "codex_key",
            () => L("cfg_codex_key"), () => codexKey, v => codexKey = v, () => L("cfg_codex_key_tip"));

        ModHelper.AddDebugFunction(ModId, ModHelper.DEBUG_CATEGORY_MODDING,
            "Open Item Codex", () => ItemCodex.Toggle());

        host = CodexHost.Create();
        hooksAttached = true;
        Debug.Log("[ItemCodex] OnEnable: hooks attached");
    }

    public void OnDisable()
    {
        if (hooksAttached)
        {
            ModHook.OnGameLoadedNormal -= OnGameLoaded;
            ModHook.OnNewGameStarted -= OnNewGameStarted;
            ModHook.OnGoingSleepLate -= OnGoingSleep;
            ModHelper.RemoveGameOptions(ModId);
            ModHelper.RemoveDebugFunctions(ModId);

            if (host != null)
            {
                UnityEngine.Object.Destroy(host.gameObject);
                host = null;
            }
            ItemCodex.OnTornDown();
            CodexSeen.OnTornDown();
            hooksAttached = false;
        }
        Debug.Log("[ItemCodex] OnDisable: hooks detached");
    }

    private void OnGameLoaded()
    {
        CodexSeen.Restore();
        ItemCodex.OnSceneMaybeRebuilt(); // 读档可能重建了场景，清理悬空的窗口引用
    }

    private void OnNewGameStarted(string scenarioId)
    {
        CodexSeen.Reset();
    }

    private void OnGoingSleep()
    {
        CodexSeen.Persist();
    }
}
