using System.Reflection;
using HarmonyLib;
using UnityEngine;

// ============================================================================
// 快速卷帘门 —— 在 OpenShutter / EndAndClose 执行前改写私有 duration 字段。
// 补丁类由游戏 ModLoader 在 Init 之前自动 PatchAll 应用。
// ============================================================================

[HarmonyPatch]
internal static class ShutterSpeedPatches
{
    private static FieldInfo durationField;

    private static void Apply(StoreShutterButton button)
    {
        if (button == null)
        {
            return;
        }

        float speed = 2.75f; // 原版值；MOD 关闭时恢复原版手感
        FastShutterEntry mod = FastShutterEntry.Instance;
        if (mod != null && mod.fastShutter != null && mod.fastShutter.Value)
        {
            float v = (mod.shutterSpeed != null) ? mod.shutterSpeed.Value : 0.6f;
            if (v > 0.05f)
            {
                speed = Mathf.Clamp(v, 0.15f, 2.75f);
            }
        }

        if (durationField == null)
        {
            durationField = AccessTools.Field(typeof(StoreShutterButton), "duration");
        }
        durationField?.SetValue(button, speed);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(StoreShutterButton), "OpenShutter")]
    internal static void PrefixOpenShutter(StoreShutterButton __instance)
    {
        Apply(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(StoreShutterButton), "EndAndClose")]
    internal static void PrefixEndAndClose(StoreShutterButton __instance)
    {
        Apply(__instance);
    }
}
