using HarmonyLib;

// ============================================================================
// 交易埋点：OnItemBought(gameItem, cost) —— 记录获得日期（持有天数用）。
// 进价本身游戏已通过 GeneralHelper.TrackBuyPrice 写入 BOUGHT_PRICE_TAG。
// ============================================================================

[HarmonyPatch(typeof(PlayerStore), "OnItemBought")]
internal static class PlayerStore_OnItemBought_Patch
{
    private static void Postfix(GameItem gameItem)
    {
        if (gameItem == null)
        {
            return;
        }
        AcqDays.Record(gameItem.GetUniqueID(), StoreStation.GetDayCounter());
    }
}
