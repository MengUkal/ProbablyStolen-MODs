using System;
using UnityEngine;

// ============================================================================
// 顾客预览 HUD (customer.hud)
//   - 右上角顾客面板：下一位顾客头像 + 名字 + 排队人数（固定在统计栏下方）
//   - 跳过当前顾客快捷键（等同点击"送客"）
//   今日经营统计已移至利润小账本（profit.ledger），两个 MOD 相互独立。
// ============================================================================

public class CustomerHudEntry : IMod
{
    public const string ModId = "customer.hud";

    public static CustomerHudEntry Instance { get; private set; }
    public ModManifest Manifest { get; private set; }

    internal ConfigEntry<bool> customerHud;
    internal KeyCode skipKey = KeyCode.None;

    private HudHost host;
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
        customerHud = cfg.Define(
            "CustomerHud", true, "Hud",
            () => L("cfg_customer_hud"), () => L("cfg_customer_hud_tip"));

        Debug.Log($"[CustomerHud] Init: {manifest.Name} v{manifest.ModVersion}");
    }

    public void OnEnable()
    {
        ModHelper.AddGameOptionSection(ModId, "hud", () => L("cfg_section"));
        ModHelper.AddGameOptionToggle(ModId, "hud", "hud_customer",
            () => L("cfg_customer_hud"), () => customerHud.Value, v => customerHud.Set(v), () => L("cfg_customer_hud_tip"));
        ModHelper.AddGameOptionKeyBind(ModId, "hud", "hud_skip_key",
            () => L("cfg_skip_key"), () => skipKey, v => skipKey = v, () => L("cfg_skip_key_tip"));

        host = HudHost.Create();
        hooksAttached = true;
        Debug.Log("[CustomerHud] OnEnable: hooks attached");
    }

    public void OnDisable()
    {
        if (hooksAttached)
        {
            ModHelper.RemoveGameOptions(ModId);

            if (host != null)
            {
                UnityEngine.Object.Destroy(host.gameObject);
                host = null;
            }
            hooksAttached = false;
        }
        Debug.Log("[CustomerHud] OnDisable: hooks detached");
    }
}
