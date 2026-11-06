using HarmonyLib;

// ============================================================================
// 特性网格控件(StartingPerkElement)只认 StartingPerkIconLoader.perkIcons 字典，
// 而该字典在原版 Start() 里每次都会 Clear 后重填 vanilla 图标——MOD 注入的图标
// 会被冲掉。此 postfix 在每次原版填充完成后把 MOD 天赋图标重新注入。
// ============================================================================

[HarmonyPatch(typeof(StartingPerkIconLoader), "Start")]
internal static class StartingPerkIconLoader_Start_Patch
{
    private static void Postfix()
    {
        StartPackEntry.InstallPerkIcons();
    }
}
