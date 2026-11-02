using System;

// ============================================================================
// 今日经营统计基准：卷帘门打开（新的一天开始）时记录 RunStats 累计值，
// 右上角统计栏显示 售出/购入/商品毛利 的"今日增量"。
// ============================================================================

public static class DailyStats
{
    public static int BaseSales;
    public static int BaseSpent;

    public static void CaptureBase()
    {
        try
        {
            RunStats rs = RunStats.Current;
            if (rs == null)
            {
                return;
            }
            BaseSales = rs.totalSales;
            BaseSpent = rs.stockSpent;
        }
        catch (Exception)
        {
        }
    }

    public static long SoldToday()
    {
        RunStats rs = RunStats.Current;
        return (rs == null) ? 0 : (rs.totalSales - BaseSales);
    }

    public static long BoughtToday()
    {
        RunStats rs = RunStats.Current;
        return (rs == null) ? 0 : (rs.stockSpent - BaseSpent);
    }
}
