using System;
using UnityEngine;

// ============================================================================
// 快速卷帘门 (fast.shutter)
//   StoreShutterButton.duration 是私有字段（原版 2.75 秒），控制：
//   - 开张/打烊的卷帘动画（LerpShutter）
//   - 打烊后"隐藏前台/送走最后一个顾客/关门音效"的延迟（WaitAndDo）
//   在 OpenShutter / EndAndClose 执行前改写该字段即可整体加速打烊流程。
//   MOD 关闭或开关关闭时自动恢复原版 2.75 秒。
// ============================================================================

public class FastShutterEntry : IMod
{
    public const string ModId = "fast.shutter";

    public static FastShutterEntry Instance { get; private set; }
    public ModManifest Manifest { get; private set; }

    internal ConfigEntry<bool> fastShutter;
    internal ConfigEntry<float> shutterSpeed;

    public static string L(string key, params object[] args)
    {
        return ModHelper.GetLocalized(ModId, key, args);
    }

    public void Init(ModManifest manifest)
    {
        Instance = this;
        Manifest = manifest;

        ModConfig cfg = ModHelper.GetConfig(ModId);
        fastShutter = cfg.Define(
            "FastShutter", true, "Shutter",
            () => L("cfg_fast_shutter"), () => L("cfg_fast_shutter_tip"));
        shutterSpeed = cfg.Define(
            "ShutterSpeed", 0.6f, "Shutter",
            () => L("cfg_shutter_speed"), 0.15f, 2.75f,
            () => L("cfg_shutter_speed_tip"));

        Debug.Log($"[FastShutter] Init: {manifest.Name} v{manifest.ModVersion}");
    }

    public void OnEnable()
    {
        ModHelper.AddGameOptionSection(ModId, "shutter", () => L("cfg_section"));
        ModHelper.AddGameOptionToggle(ModId, "shutter", "shutter_fast",
            () => L("cfg_fast_shutter"), () => fastShutter.Value, v => fastShutter.Set(v), () => L("cfg_fast_shutter_tip"));
        ModHelper.AddGameOptionSlider(ModId, "shutter", "shutter_speed",
            () => L("cfg_shutter_speed"), () => shutterSpeed.Value, v => shutterSpeed.Set(v),
            0.15f, 2.75f, false, () => L("cfg_shutter_speed_tip"));

        Debug.Log("[FastShutter] OnEnable: hooks attached");
    }

    public void OnDisable()
    {
        ModHelper.RemoveGameOptions(ModId);
        Debug.Log("[FastShutter] OnDisable: hooks detached");
    }
}
