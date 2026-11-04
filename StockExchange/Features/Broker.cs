using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// 交易逻辑：买入/卖出/开空/平空/限价委托/手续费/持仓上限/账户等级
//   - 账户等级由暗网升级推导（接入=1 / 量化终端=2 / 做空执照=3）
//   - 现金直接走 PlayerStore.ModCash（±），与原版资金同账本
//   - 手续费单边 1%（最低 5），计入已实现盈亏、不资本化到均价
//   - P2 做空（策划案 §6.4 定稿）：开空=卖借来的股（现金不动，只扣费）；
//     借券费 0.5%/日；开空保证金 150%（现金+持仓净值 ≥ 空头总市值×150%）；
//     强平线 120%（现金+持仓净值+空头浮盈 < 空头市值×120% 时全部按收盘平掉）。
//     现金不足扣费/平空亏损时扣到 0 为止（差额计入已实现盈亏，不记欠款）。
//   - P2 限价委托：挂单（买/卖/开空/平空 × 限价 × 数量），
//     每日结算按当日新收盘价撮合（穿过限价即成交，滑点 0），校验失败作废。
// ============================================================================

public static class Broker
{
    // ---- P2 定稿参数（策划案 §12 锁定，不进 ModConfig）----
    public const float BorrowRatePerDay = 0.005f; // 借券费 0.5%/日
    public const float MarginOpenRatio = 1.50f;   // 开空保证金 150%
    public const float MarginCallRatio = 1.20f;   // 强平线 120%
    public const int MaxPendingOrders = 20;       // 委托上限（防 UI 无限膨胀）

    private static readonly List<string> notices = new List<string>();

    /// 结算/成交通知（已本地化）。UI 刷新时取走显示，同时落 Debug.Log。
    public static void PushNotice(string text)
    {
        notices.Add(text);
        Debug.Log("[OrbitExchange] " + text);
    }

    /// 取走全部通知并清空（一次显示），无通知返回 null。
    public static string DrainNotices()
    {
        if (notices.Count == 0)
        {
            return null;
        }
        string s = string.Join(" / ", notices.ToArray());
        notices.Clear();
        return s;
    }
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

    /// 浮动盈亏（未实现，不含费；多头+空头口径）。
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
            if (st.shortQty[i] > 0)
            {
                sum += Mathf.RoundToInt((st.shortAvgCost[i] - st.prices[i]) * st.shortQty[i]);
            }
        }
        return sum;
    }

    /// 今日盈亏（相对昨收，未实现；多头+空头口径）。
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
            if (st.shortQty[i] > 0)
            {
                sum += Mathf.RoundToInt((st.prevClose[i] - st.prices[i]) * st.shortQty[i]);
            }
        }
        return sum;
    }

    // ================================================================ P2 做空

    /// 多头持仓总市值（现金+净值口径用）。
    public static int HoldingsMarketValue(ExchangeState st)
    {
        int sum = 0;
        for (int i = 0; i < MarketCatalog.Count; i++)
        {
            if (st.qty[i] > 0)
            {
                sum += Mathf.RoundToInt(st.qty[i] * st.prices[i]);
            }
        }
        return sum;
    }

    /// 空头总市值。
    public static int ShortMarketValue(ExchangeState st)
    {
        int sum = 0;
        for (int i = 0; i < MarketCatalog.Count; i++)
        {
            if (st.shortQty[i] > 0)
            {
                sum += Mathf.RoundToInt(st.shortQty[i] * st.prices[i]);
            }
        }
        return sum;
    }

    /// 空头浮动盈亏（价格下跌为正）。
    public static int ShortFloatPnl(ExchangeState st)
    {
        int sum = 0;
        for (int i = 0; i < MarketCatalog.Count; i++)
        {
            if (st.shortQty[i] > 0)
            {
                sum += Mathf.RoundToInt((st.shortAvgCost[i] - st.prices[i]) * st.shortQty[i]);
            }
        }
        return sum;
    }

    /// 开空。out requiredMargin = 开空后的保证金要求（空头总市值×150%）。
    /// 现金不动（只扣开仓手续费）；空头市值计入单股持仓上限。
    public static string TryOpenShort(int idx, int shares, out int requiredMargin)
    {
        requiredMargin = 0;
        if (!InGame() || idx < 0 || idx >= MarketCatalog.Count || shares <= 0)
        {
            return "msg_bad_qty";
        }
        var st = MarketData.State;
        StockDef def = MarketCatalog.Stocks[idx];
        int tier = Tier();
        if (tier < 3)
        {
            return "ui_need_short";
        }
        if (def.minTier > tier)
        {
            return def.minTier == 1 ? "ui_need_access" : "ui_need_quant";
        }
        if (shares > 999999)
        {
            return "msg_bad_qty";
        }

        float price = st.prices[idx];
        float newShortMv = st.shortQty[idx] * price + price * shares;
        int cap = CapFor(tier);
        if (newShortMv > cap)
        {
            return "msg_cap";
        }

        // 保证金：现金 + 持仓净值 ≥ 空头总市值×150%（定稿口径，不含空头浮盈）
        PlayerStore ps = PlayerStore.Instance;
        int equity = Mathf.RoundToInt(ps.playerCash) + HoldingsMarketValue(st);
        requiredMargin = Mathf.RoundToInt(newShortMv * MarginOpenRatio);
        if (equity < requiredMargin)
        {
            return "msg_no_margin";
        }

        int fee = FeeOf(Mathf.RoundToInt(price * shares));
        int cash = Mathf.RoundToInt(ps.playerCash);
        int realFee = Mathf.Min(fee, cash); // 现金不足扣到 0 为止
        if (realFee > 0)
        {
            ps.ModCash(-realFee, true);
        }
        st.realized -= realFee;

        st.shortAvgCost[idx] = (st.shortAvgCost[idx] * st.shortQty[idx] + price * shares)
            / (st.shortQty[idx] + shares);
        st.shortQty[idx] += shares;
        st.trades++;
        MarketData.Persist();
        return null;
    }

    /// 平空（买入归还）。shares<=0 或 closeAll=true 时全平。
    /// out pnl = (开空均价 - 现价)×数量 - 费用。
    public static string TryCloseShort(int idx, int shares, bool closeAll, out int pnl)
    {
        pnl = 0;
        if (!InGame() || idx < 0 || idx >= MarketCatalog.Count)
        {
            return "msg_bad_qty";
        }
        var st = MarketData.State;
        if (Tier() < 3)
        {
            return "ui_need_short";
        }
        if (st.shortQty[idx] <= 0)
        {
            return "msg_none";
        }
        int realShares = closeAll || shares <= 0 ? st.shortQty[idx] : Mathf.Min(shares, st.shortQty[idx]);

        float price = st.prices[idx];
        int fee = FeeOf(Mathf.RoundToInt(price * realShares));
        pnl = Mathf.RoundToInt((st.shortAvgCost[idx] - price) * realShares) - fee;

        PlayerStore ps = PlayerStore.Instance;
        st.realized += pnl;
        int cashDelta = pnl;
        int cash = Mathf.RoundToInt(ps.playerCash);
        if (cash + cashDelta < 0)
        {
            // 亏损超出现金：现金扣到 0 为止，差额记入已实现盈亏（不记欠款）
            st.realized += cash + cashDelta;
            cashDelta = -cash;
        }
        if (cashDelta != 0)
        {
            ps.ModCash(cashDelta, true);
        }

        st.shortQty[idx] -= realShares;
        if (st.shortQty[idx] == 0)
        {
            st.shortAvgCost[idx] = 0f;
        }
        st.trades++;
        MarketData.Persist();
        return null;
    }

    /// 最大可开空股数（空头市值上限与保证金双重约束）。
    public static int MaxShortable(int idx, int tier)
    {
        if (!InGame() || idx < 0)
        {
            return 0;
        }
        var st = MarketData.State;
        float price = Mathf.Max(1f, st.prices[idx]);
        int cap = CapFor(tier);
        if (cap <= 0 || tier < 3)
        {
            return 0;
        }
        int byCap = Mathf.FloorToInt((cap - st.shortQty[idx] * price) / price);
        int equity = Mathf.RoundToInt(PlayerStore.Instance.playerCash) + HoldingsMarketValue(st);
        int byMargin = Mathf.FloorToInt((equity / MarginOpenRatio - st.shortQty[idx] * price) / price);
        return Mathf.Max(0, Mathf.Min(byCap, byMargin));
    }

    // ================================================================ P2 限价委托

    /// 挂限价委托。side: 0=买 1=卖 2=开空 3=平空。
    public static string PlaceOrder(int idx, int side, int shares, float limit)
    {
        if (!InGame() || idx < 0 || idx >= MarketCatalog.Count || shares <= 0 || limit <= 0f)
        {
            return "msg_bad_qty";
        }
        var st = MarketData.State;
        StockDef def = MarketCatalog.Stocks[idx];
        int tier = Tier();
        if ((side == 2 || side == 3) && tier < 3)
        {
            return "ui_need_short";
        }
        if (def.minTier > tier)
        {
            return def.minTier == 1 ? "ui_need_access" : "ui_need_quant";
        }
        if (shares > 999999 || side < 0 || side > 3)
        {
            return "msg_bad_qty";
        }
        if (st.orders == null)
        {
            st.orders = new List<PendingOrder>();
        }
        if (st.orders.Count >= MaxPendingOrders)
        {
            return "msg_order_max";
        }
        st.orders.Add(new PendingOrder { idx = idx, side = side, shares = shares, limit = limit });
        MarketData.Persist();
        return null;
    }

    /// 撤销第 k 条委托（UI 以当前列表下标调用）。
    public static string CancelOrder(int k)
    {
        if (!InGame())
        {
            return "msg_bad_qty";
        }
        var st = MarketData.State;
        if (st.orders == null || k < 0 || k >= st.orders.Count)
        {
            return "msg_bad_qty";
        }
        st.orders.RemoveAt(k);
        MarketData.Persist();
        return null;
    }

    /// 每日结算末尾调用（新收盘价已生效）：撮合 → 借券费 → 强平。
    public static void OnDaySettled()
    {
        if (!InGame())
        {
            return; // 场景切换期跳过（下个结算日再处理，不双扣不重算）
        }
        var st = MarketData.State;
        MatchPendingOrders(st);
        ChargeBorrowFee(st);
        CheckMarginCall(st);
    }

    /// 按当日收盘价撮合：买/平空穿下限成交，卖/开空穿上限成交。
    /// 成交调用市价逻辑（滑点 0）；校验失败的委托作废并提示。
    private static void MatchPendingOrders(ExchangeState st)
    {
        if (st.orders == null || st.orders.Count == 0)
        {
            return;
        }
        for (int k = st.orders.Count - 1; k >= 0; k--)
        {
            PendingOrder o = st.orders[k];
            if (o.idx < 0 || o.idx >= MarketCatalog.Count)
            {
                st.orders.RemoveAt(k);
                continue;
            }
            float p = st.prices[o.idx];
            string code = MarketCatalog.Stocks[o.idx].code;
            string sideName = StockExchangeEntry.L("side_" + o.side);
            bool cross = (o.side == 0 || o.side == 3) ? p <= o.limit : p >= o.limit;
            if (!cross)
            {
                continue;
            }

            // 卖出/平空数量超出持仓时直接作废（不做部分成交）
            if (o.side == 1 && st.qty[o.idx] < o.shares)
            {
                PushNotice(StockExchangeEntry.L("msg_order_void",
                    StockExchangeEntry.L("msg_none"), sideName, o.shares.ToString("N0"), code));
                st.orders.RemoveAt(k);
                continue;
            }
            if (o.side == 3 && st.shortQty[o.idx] < o.shares)
            {
                PushNotice(StockExchangeEntry.L("msg_order_void",
                    StockExchangeEntry.L("msg_none"), sideName, o.shares.ToString("N0"), code));
                st.orders.RemoveAt(k);
                continue;
            }

            string err;
            switch (o.side)
            {
                case 0: { int paid; err = TryBuy(o.idx, o.shares, out paid); break; }
                case 1: { int got; err = TrySell(o.idx, o.shares, false, out got); break; }
                case 2: { int margin; err = TryOpenShort(o.idx, o.shares, out margin); break; }
                default: { int pnl; err = TryCloseShort(o.idx, o.shares, false, out pnl); break; }
            }

            if (err == null)
            {
                PushNotice(StockExchangeEntry.L("msg_order_filled",
                    sideName, o.shares.ToString("N0"), code, o.limit.ToString("0.0")));
            }
            else
            {
                PushNotice(StockExchangeEntry.L("msg_order_void",
                    StockExchangeEntry.L(err), sideName, o.shares.ToString("N0"), code));
            }
            st.orders.RemoveAt(k);
        }
    }

    /// 借券费：0.5%/日 × 空头市值，现金扣到 0 为止，计入已实现盈亏。
    private static void ChargeBorrowFee(ExchangeState st)
    {
        int owed = 0;
        for (int i = 0; i < MarketCatalog.Count; i++)
        {
            if (st.shortQty[i] > 0)
            {
                owed += Mathf.RoundToInt(st.shortQty[i] * st.prices[i] * BorrowRatePerDay);
            }
        }
        if (owed <= 0)
        {
            return;
        }
        PlayerStore ps = PlayerStore.Instance;
        int real = Mathf.Min(owed, Mathf.RoundToInt(ps.playerCash));
        if (real > 0)
        {
            ps.ModCash(-real, true);
        }
        st.realized -= real;
        if (real < owed)
        {
            Debug.LogWarning($"[OrbitExchange] 借券费 {owed} 超出现金，实扣 {real}（扣到 0 为止）");
        }
        else
        {
            Debug.Log($"[OrbitExchange] 借券费扣款 {real}");
        }
    }

    /// 强平检查：现金+持仓净值+空头浮盈 < 空头市值×120% 时，全部空头按当日收盘平掉。
    private static void CheckMarginCall(ExchangeState st)
    {
        int shortMv = ShortMarketValue(st);
        if (shortMv <= 0)
        {
            return;
        }
        PlayerStore ps = PlayerStore.Instance;
        int equity = Mathf.RoundToInt(ps.playerCash) + HoldingsMarketValue(st) + ShortFloatPnl(st);
        if ((float)equity >= shortMv * MarginCallRatio)
        {
            return;
        }
        PushNotice(StockExchangeEntry.L("msg_margin_call"));
        for (int i = 0; i < MarketCatalog.Count; i++)
        {
            if (st.shortQty[i] > 0)
            {
                int pnl;
                TryCloseShort(i, 0, true, out pnl);
            }
        }
    }
}
