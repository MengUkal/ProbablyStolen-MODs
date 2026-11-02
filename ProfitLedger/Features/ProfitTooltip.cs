using System;
using UnityEngine;

// ============================================================================
// 利润小账本 —— 在 OnCreateTooltipLate 追加增量信息：
//   利润：现估值 - 进价（含利润率），绿涨红跌
//   已持有 N 天
// 游戏原生 Tooltip 已显示 现估值/基准价/进价（GameItemElement.cs:1012-1026），
// 所以这里只补充"差值"信息，不重复。进价来自游戏原生 BOUGHT_PRICE_TAG。
// ============================================================================

public static class ProfitTooltip
{
    public static void OnTooltip(RichTextBuilder builder, GameItem item)
    {
        ProfitLedgerEntry mod = ProfitLedgerEntry.Instance;
        if (mod == null || builder == null || item == null)
        {
            return;
        }
        if (mod.profitLine == null || !mod.profitLine.Value)
        {
            return;
        }
        if (!PlayerStore.HasInstance)
        {
            return;
        }

        try
        {
            if (!GeneralHelper.IsItemOwned(item))
            {
                return; // 只统计自己的库存，顾客放在柜台上的物品不显示
            }

            int bought = item.GetTagReadonly("BOUGHT_PRICE_TAG").GetInt();
            if (bought <= 0)
            {
                return; // 没有进价记录（如赠送/抽奖获得的物品）
            }

            long current = item.GetCurrentValue();
            long profit = current - bought;
            int pct = (int)Math.Round(100.0 * profit / bought);

            RenderHandler.ColorPalette palette = (profit >= 0) ? RenderHandler.ColorPalette.Green : RenderHandler.ColorPalette.Red;
            builder.AddLine(ProfitLedgerEntry.L("tt_profit", profit, pct), true, palette, true);

            if (mod.heldDaysLine != null && mod.heldDaysLine.Value)
            {
                int days = AcqDays.GetDays(item.GetUniqueID());
                if (days >= 0)
                {
                    builder.AddLine(ProfitLedgerEntry.L("tt_days", days), true, RenderHandler.ColorPalette.None, false);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ProfitLedger] ProfitTooltip failed: " + e.Message);
        }
    }
}
