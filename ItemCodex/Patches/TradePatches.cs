using HarmonyLib;

// ============================================================================
// 交易埋点（图鉴"已见过"标记）：
//   OnItemBought —— 买到的物品标记为已见过
//   SellItem     —— 卖出的物品标记为已见过
// ============================================================================

[HarmonyPatch(typeof(PlayerStore), "OnItemBought")]
internal static class PlayerStore_OnItemBought_Patch
{
    private static void Postfix(GameItem gameItem)
    {
        if (gameItem != null)
        {
            CodexSeen.Add(gameItem.identifier);
        }
    }
}

[HarmonyPatch(typeof(PlayerStore), "SellItem")]
internal static class PlayerStore_SellItem_Patch
{
    private static void Postfix(GameItem item)
    {
        if (item != null)
        {
            CodexSeen.Add(item.identifier);
        }
    }
}
