using UnityEngine;

// ============================================================================
// 交易逻辑：买入/卖出/手续费/持仓上限/账户等级
//   - 账户等级由暗网升级推导（接入=1 / 量化终端=2 / 做空执照=3）
//   - 现金直接走 PlayerStore.ModCash（±），与原版资金同账本
//   - 手续费单边 1%（最低 5），计入已实现盈亏、不资本化到均价
// ============================================================================

public static class Broker
{
    public static int CapFor(int tier)
    {
        switch (tier)
        {
            case 3: return 20000;
            case 2: return 10000;
            case 1: return 5000;
            default: return 0;
        }
    }

    public static int Tier()
    {
        try
        {
            if (ModHelper.IsNetworkUpgradeUnlocked(StockExchangeEntry.UpShort)) return 3;
            if (ModHelper.IsNetworkUpgradeUnlocked(StockExchangeEntry.UpQuant)) return 2;
            if (ModHelper.IsNetworkUpgradeUnlocked(StockExchangeEntry.UpAccess)) return 1;
            return 0;
        }
        catch
        {
            return 0; // 菜单期 PlayerStore 惰性构造会抛异常；仅在游戏内调用时才可能得到真实等级
        }
    }

    public static bool InGame()
    {
        try
        {
            // 场景哨兵：StoreUIManager 是 Awake 赋值的普通场景单例，主菜单期安全为 null、访问无副作用。
            // 不能用 StoreStation.Instance / PlayerStore.Instance 判局内——两者都是惰性构造单例，
            // 菜单期访问会构造出新对象（污染游戏自身对 Instance==null 的判断）甚至 NRE。
            if (StoreUIManager.Instance == null)
            {
                return false;
            }
            return PlayerStore.Instance != null && MarketData.IsReady;
        }
        catch
        {
            return false;
        }
    }

    private static int FeeOf(int amount)
    {
        var e = StockExchangeEntry.Instance;
        float rate = e != null ? e.FeeRate() : 0.01f;
        return Mathf.Max(5, Mathf.RoundToInt(amount * rate));
    }

    /// 买入。返回 null 表示成功（totalPaid = 实付含费），否则返回错误本地化 key。
    public static string TryBuy(int idx, int shares, out int totalPaid)
    {
        totalPaid = 0;
        if (!InGame() || idx < 0 || idx >= MarketCatalog.Count || shares <= 0)
        {
            return "msg_bad_qty";
        }
        var st = MarketData.State;
        StockDef def = MarketCatalog.Stocks[idx];
        int tier = Tier();
        if (def.minTier > tier)
        {
            return def.minTier == 1 ? "ui_need_access" : "ui_need_quant";
        }
        if (shares > 999999)
        {
            return "msg_bad_qty";
        }

        float price = st.prices[idx];
        float cost = price * shares;
        int fee = FeeOf(Mathf.RoundToInt(cost));
        int total = Mathf.RoundToInt(cost) + fee;

        int cap = CapFor(tier);
        if ((st.qty[idx] + shares) * price > cap)
        {
            return "msg_cap";
        }
        PlayerStore ps = PlayerStore.Instance;
        if (!ps.HasCredit(total))
        {
            return "msg_no_cash";
        }

        ps.ModCash(-total, true);
        float newAvg = (st.avgCost[idx] * st.qty[idx] + price * shares) / (st.qty[idx] + shares);
        st.qty[idx] += shares;
        st.avgCost[idx] = newAvg;
        st.trades++;
        MarketData.Persist();
        totalPaid = total;
        return null;
    }

    /// 卖出。shares<=0 或 sellAll=true 时清仓。返回 null 表示成功。
    public static string TrySell(int idx, int shares, bool sellAll, out int proceeds)
    {
        proceeds = 0;
        if (!InGame() || idx < 0 || idx >= MarketCatalog.Count)
        {
            return "msg_bad_qty";
        }
        var st = MarketData.State;
        StockDef def = MarketCatalog.Stocks[idx];
        if (def.minTier > Tier())
        {
            return def.minTier == 1 ? "ui_need_access" : "ui_need_quant";
        }
        if (st.qty[idx] <= 0)
        {
            return "msg_none";
        }
        int realShares = sellAll || shares <= 0 ? st.qty[idx] : Mathf.Min(shares, st.qty[idx]);

        float price = st.prices[idx];
        int gross = Mathf.RoundToInt(price * realShares);
        int fee = FeeOf(gross);
        proceeds = Mathf.Max(0, gross - fee);

        PlayerStore.Instance.ModCash(proceeds, true);
        st.realized += Mathf.RoundToInt((price - st.avgCost[idx]) * realShares) - fee;
        st.qty[idx] -= realShares;
        if (st.qty[idx] == 0)
        {
            st.avgCost[idx] = 0f;
        }
        st.trades++;
        MarketData.Persist();
        return null;
    }

    /// 最大可买股数（受现金与单股持仓市值上限双重约束）。
    public static int MaxAffordable(int idx, int tier)
    {
        if (!InGame() || idx < 0)
        {
            return 0;
        }
        var st = MarketData.State;
        float price = Mathf.Max(1f, st.prices[idx]);
        int cap = CapFor(tier);
        if (cap <= 0)
        {
            return 0;
        }
        int byCap = Mathf.FloorToInt((cap - st.qty[idx] * price) / price);
        int byCash = Mathf.FloorToInt((PlayerStore.Instance.playerCash - 10) / (price * (1f + StockExchangeEntry.Instance.FeeRate())));
        return Mathf.Max(0, Mathf.Min(byCap, byCash));
    }

    /// 浮动盈亏（未实现，不含费）。
    public static int UnrealizedPnl()
    {
        if (!InGame())
        {
            return 0;
        }
        var st = MarketData.State;
        int sum = 0;
        for (int i = 0; i < MarketCatalog.Count; i++)
        {
            if (st.qty[i] > 0)
            {
                sum += Mathf.RoundToInt((st.prices[i] - st.avgCost[i]) * st.qty[i]);
            }
        }
        return sum;
    }

    /// 今日盈亏（相对昨收，未实现）。
    public static int DayPnl()
    {
        if (!InGame())
        {
            return 0;
        }
        var st = MarketData.State;
        int sum = 0;
        for (int i = 0; i < MarketCatalog.Count; i++)
        {
            if (st.qty[i] > 0)
            {
                sum += Mathf.RoundToInt((st.prices[i] - st.prevClose[i]) * st.qty[i]);
            }
        }
        return sum;
    }
}
