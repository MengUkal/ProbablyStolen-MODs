using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// ============================================================================
// 环带交易所窗口（官方 CustomUI 框架，ItemCodex 同款模式）
//   顶部：交易日 / 现金 / 今日盈亏 / 账户等级
//   列表：8 只股票（代码·名称·现价·日涨跌，锁定的灰显）
//   详情：公司简介 / 事件预警(量化终端) / 像素K线(量化终端) / 持仓 / 买卖
// ============================================================================

public static class ExchangeUI
{
    private static CustomUIWindow window;
    private static GameObject windowRoot;
    private static RectTransform listContent;
    private static RectTransform detailHost;

    private static CustomUIElement dayEl;
    private static CustomUIElement cashEl;
    private static CustomUIElement pnlEl;
    private static CustomUIElement tierEl;
    private static CustomUIElement msgEl;
    private static CustomUIElement qtyEl;

    private static int selected = -1;
    private static string qtyText = "10";
    private static Texture2D chartTex;

    private static readonly Color UpColor = new Color(0.30f, 0.90f, 0.45f);
    private static readonly Color DownColor = new Color(0.95f, 0.36f, 0.36f);
    private static readonly Color FlatColor = new Color(0.62f, 0.62f, 0.62f);
    private static readonly Color LockedColor = new Color(0.45f, 0.45f, 0.45f);

    // ---------------------------------------------------------------- 开关

    public static bool IsVisible
    {
        get { return window != null && windowRoot != null && windowRoot.activeSelf; }
    }

    public static void Toggle()
    {
        if (!Broker.InGame())
        {
            return;
        }
        if (window != null && windowRoot != null)
        {
            window.Toggle();
            if (IsVisible)
            {
                RefreshAll();
            }
            return;
        }
        Build();
    }

    /// 读档可能重建场景；窗口悬空时清理引用以便重建。
    public static void OnSceneMaybeRebuilt()
    {
        if (windowRoot == null)
        {
            OnTornDown();
        }
    }

    public static void OnTornDown()
    {
        window = null;
        windowRoot = null;
        listContent = null;
        detailHost = null;
        dayEl = cashEl = pnlEl = tierEl = msgEl = qtyEl = null;
        ChartTexture.Destroy(ref chartTex);
    }

    // ---------------------------------------------------------------- 构建

    private static void Build()
    {
        if (CustomUIManager.Instance == null)
        {
            Debug.Log("[OrbitExchange] 窗口不可用：场景中无 CustomUIManager");
            return;
        }
        try
        {
            CustomUIBuilder b = CustomUIManager.Window("orbit_exchange", StockExchangeEntry.L("ui_title"));
            b.SetSize(960f, 640f).Center().SetCloseOnEscape().SetDraggable(true);

            // ---- 顶部状态行（标签灰、数值白）----
            b.BeginRow();
            dayEl = AddStat(b, "se_day_lbl", StockExchangeEntry.L("ui_day"), "se_day");
            cashEl = AddStat(b, "se_cash_lbl", StockExchangeEntry.L("ui_cash"), "se_cash");
            pnlEl = AddStat(b, "se_pnl_lbl", StockExchangeEntry.L("ui_pnl"), "se_pnl");
            tierEl = AddStat(b, "se_tier_lbl", StockExchangeEntry.L("ui_tier"), "se_tier");
            b.End();

            b.AddSpace(6f);

            // ---- 主区：左行情列表 / 右详情 ----
            b.BeginRow();
            b.BeginPanel(312f, -1f, TextAnchor.UpperLeft, 4f, 2f, "se_list_panel");
            b.AddHeader(StockExchangeEntry.L("ui_list_header"), "se_list_header");
            b.BeginScrollFlexible();
            listContent = b.CurrentContainer;
            b.End();
            b.End();

            b.BeginPanel(-1f, -1f, TextAnchor.UpperLeft, 10f, 6f, "se_detail");
            detailHost = b.CurrentContainer;
            b.End();
            b.End();

            // ---- 底部反馈行 ----
            b.AddLabel("", "se_msg");
            msgEl = b.Last;

            window = b.Show();
            windowRoot = (window.root != null) ? window.root.gameObject : null;
            window.onClose = () => { /* 仅隐藏，保留窗口以便秒开 */ };

            RefreshAll();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[OrbitExchange] 窗口构建失败: " + e.Message);
            OnTornDown();
        }
    }

    /// 顶部状态项：灰色标签 + 白色数值，返回数值元素。
    private static CustomUIElement AddStat(CustomUIBuilder b, string lblId, string lbl, string valId)
    {
        b.AddLabel(lbl, lblId);
        b.Last.SetTextColor(FlatColor);
        b.AddLabel("", valId);
        return b.Last;
    }

    // ---------------------------------------------------------------- 刷新

    public static void RefreshAll()
    {
        if (window == null || windowRoot == null || !Broker.InGame())
        {
            return;
        }
        if (selected < 0 || selected >= MarketCatalog.Count)
        {
            selected = 0;
        }
        RefreshTop();
        RebuildList();
        ShowDetail();
    }

    private static void RefreshTop()
    {
        var st = MarketData.State;
        if (st == null || dayEl == null)
        {
            return;
        }
        try
        {
            dayEl.SetText("D" + StockExchangeEntry.GetDay());
            cashEl.SetText(PlayerStore.Instance.playerCash.ToString("N0"));

            int dayPnl = Broker.DayPnl();
            pnlEl.SetText(Signed(dayPnl));
            pnlEl.SetTextColor(SignColor(dayPnl));

            int tier = Broker.Tier();
            tierEl.SetText("T" + tier + " " + StockExchangeEntry.L("tier" + tier)
                + " · " + StockExchangeEntry.L("ui_cap", Broker.CapFor(tier).ToString("N0")));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[OrbitExchange] RefreshTop: " + e.Message);
        }
    }

    // ---------------------------------------------------------------- 列表

    private static void RebuildList()
    {
        if (listContent == null)
        {
            return;
        }
        CustomUIBuilder.ClearContainer(listContent);
        var st = MarketData.State;
        if (st == null)
        {
            return;
        }
        int tier = Broker.Tier();

        // 排序：可交易的在前、锁定的在后（组内保持原顺序）
        List<int> order = new List<int>();
        for (int i = 0; i < MarketCatalog.Count; i++)
        {
            if (MarketCatalog.Stocks[i].minTier <= tier)
            {
                order.Add(i);
            }
        }
        for (int i = 0; i < MarketCatalog.Count; i++)
        {
            if (MarketCatalog.Stocks[i].minTier > tier)
            {
                order.Add(i);
            }
        }

        foreach (int i in order)
        {
            int idx = i;
            StockDef def = MarketCatalog.Stocks[idx];
            bool locked = def.minTier > tier;
            float p = st.prices[idx];
            float pc = Mathf.Max(0.01f, st.prevClose[idx]);
            float chg = (p - pc) / pc;

            string arrow = chg > 0.0005f ? "▲" : (chg < -0.0005f ? "▼" : "·");
            string label = (idx == selected ? "▶ " : "") + def.code + " "
                + StockExchangeEntry.L("st_" + def.code + "_name")
                + "   " + p.ToString("0.0") + " " + arrow + PctText(chg)
                + (locked ? " · " + StockExchangeEntry.L("ui_locked") : "");

            try
            {
                CustomUIBuilder rb = new CustomUIBuilder(CustomUIManager.Instance, window, listContent);
                rb.AddButton(label, () => Select(idx), "se_row_" + idx);
                if (rb.Last != null)
                {
                    // 统一行宽 + 方向底色（绿/红淡染、锁定暗灰）
                    rb.Last.SetFixedWidth(292f);
                    Color bg = locked
                        ? new Color(1f, 1f, 1f, 0.05f)
                        : (chg > 0.0005f ? new Color(0.45f, 0.9f, 0.5f, 0.16f)
                        : (chg < -0.0005f ? new Color(1f, 0.45f, 0.45f, 0.16f)
                        : new Color(1f, 1f, 1f, 0.08f)));
                    rb.Last.SetImageColor(bg);
                    Color txt = locked ? LockedColor
                        : (idx == selected ? Color.white : SignColorRaw(chg));
                    rb.Last.SetTextColor(txt);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OrbitExchange] 行构建失败: " + e.Message);
            }
        }
        CustomUIBuilder.RebuildLayout(listContent);
    }

    private static void Select(int idx)
    {
        selected = idx;
        SetMsg("", FlatColor);
        RebuildList();
        ShowDetail();
    }

    // ---------------------------------------------------------------- 详情

    private static void ShowDetail()
    {
        if (detailHost == null)
        {
            return;
        }
        CustomUIBuilder.ClearContainer(detailHost);
        ChartTexture.Destroy(ref chartTex);

        var st = MarketData.State;
        if (st == null || selected < 0 || selected >= MarketCatalog.Count)
        {
            RebuildLayout();
            return;
        }

        try
        {
            StockDef def = MarketCatalog.Stocks[selected];
            int tier = Broker.Tier();
            float p = st.prices[selected];
            float pc = Mathf.Max(0.01f, st.prevClose[selected]);
            float chg = (p - pc) / pc;
            bool up = chg >= 0f;

            CustomUIBuilder rb = new CustomUIBuilder(CustomUIManager.Instance, window, detailHost);

            // ---- 标题行 ----
            rb.BeginRow();
            rb.AddHeader(def.code + " · " + StockExchangeEntry.L("st_" + def.code + "_name"));
            rb.AddLabel(p.ToString("0.0") + "  " + (up ? "▲" : "▼") + " " + PctText(chg), "se_quote");
            rb.Last.SetTextColor(SignColorRaw(chg));
            rb.End();

            // ---- 公司简介 ----
            rb.AddLabel(StockExchangeEntry.L("st_" + def.code + "_blurb"), "se_blurb");

            // ---- 事件预警（量化终端，只提示不剧透方向）----
            if (tier >= 2 && EventLink.HasFutureWarning(def.code, 4))
            {
                rb.AddWarning(StockExchangeEntry.L("ui_warn_future"), "se_warn");
            }

            // ---- 图表 + 持仓信息行（K线为基础功能）----
            rb.BeginRow();
            chartTex = null;
            Sprite chartSprite = ChartTexture.Make(MarketData.History(selected), up, out chartTex, 110, 36);
            rb.AddImage(chartSprite, 240f, 80f, "se_chart");
            rb.AddSpace(10f);
            rb.BeginColumn();
            if (st.qty[selected] > 0)
            {
                float posValue = st.qty[selected] * p;
                float pnl = (p - st.avgCost[selected]) * st.qty[selected];
                rb.AddLabel(StockExchangeEntry.L("ui_pos",
                    st.qty[selected].ToString("N0"),
                    st.avgCost[selected].ToString("0.0"),
                    posValue.ToString("N0"),
                    Signed(Mathf.RoundToInt(pnl))), "se_pos");
                var posEl = rb.Last;
                posEl.SetTextColor(SignColor(Mathf.RoundToInt(pnl)));
            }
            else
            {
                rb.AddLabel(StockExchangeEntry.L("ui_no_pos"), "se_pos");
            }
            rb.AddLabel(StockExchangeEntry.L("ui_cap_line", Broker.CapFor(tier).ToString("N0")), "se_cap");
            rb.AddLabel(StockExchangeEntry.L("ui_total_pnl",
                Signed(st.realized), st.trades.ToString("N0")), "se_realized");
            rb.End();
            rb.End();

            AddTradeRow(rb, def, tier);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[OrbitExchange] 详情构建失败: " + e.Message);
        }
        RebuildLayout();
    }

    /// 交易行（统一控件宽度）；标的未解锁时显示所需升级提示。
    private static void AddTradeRow(CustomUIBuilder rb, StockDef def, int tier)
    {
        if (def.minTier > tier)
        {
            rb.AddWarning(StockExchangeEntry.L(
                def.minTier == 1 ? "ui_need_access" : "ui_need_quant"), "se_need");
            return;
        }
        rb.BeginRow();
        rb.AddLabel(StockExchangeEntry.L("ui_qty"), "se_qty_lbl");
        rb.AddInput("", qtyText, null, "se_qty", t => qtyText = t);
        qtyEl = rb.Last;
        qtyEl.SetFixedWidth(88f);
        rb.AddButton(StockExchangeEntry.L("ui_max"), SetQtyMax, "se_btn_max");
        rb.Last.SetFixedWidth(52f);
        rb.AddButton(StockExchangeEntry.L("ui_buy"), DoBuy, "se_btn_buy");
        rb.Last.SetFixedWidth(52f);
        rb.AddButton(StockExchangeEntry.L("ui_sell"), () => DoSell(false), "se_btn_sell");
        rb.Last.SetFixedWidth(52f);
        rb.AddButton(StockExchangeEntry.L("ui_sellall"), () => DoSell(true), "se_btn_sellall");
        rb.Last.SetFixedWidth(52f);
        rb.End();
    }

    // ---------------------------------------------------------------- 交易

    private static void SetQtyMax()
    {
        int max = Broker.MaxAffordable(selected, Broker.Tier());
        if (max <= 0)
        {
            SetMsg(StockExchangeEntry.L("msg_no_cash"), DownColor);
            return;
        }
        qtyText = max.ToString();
        if (qtyEl != null)
        {
            qtyEl.SetString(qtyText, false);
        }
    }

    private static void DoBuy()
    {
        int shares = ParseQty();
        if (shares <= 0)
        {
            return;
        }
        int total;
        string err = Broker.TryBuy(selected, shares, out total);
        if (err == null)
        {
            SetMsg(StockExchangeEntry.L("msg_bought",
                shares.ToString("N0"),
                MarketCatalog.Stocks[selected].code,
                total.ToString("N0")), UpColor);
        }
        else
        {
            SetMsg(StockExchangeEntry.L(err), DownColor);
        }
        RefreshAll();
    }

    private static void DoSell(bool sellAll)
    {
        int shares = ParseQty();
        if (!sellAll && shares <= 0)
        {
            return;
        }
        int proceeds;
        string err = Broker.TrySell(selected, shares, sellAll, out proceeds);
        if (err == null)
        {
            SetMsg(StockExchangeEntry.L("msg_sold",
                (sellAll ? StockExchangeEntry.L("ui_all") : shares.ToString("N0")),
                MarketCatalog.Stocks[selected].code,
                proceeds.ToString("N0")), UpColor);
        }
        else
        {
            SetMsg(StockExchangeEntry.L(err), DownColor);
        }
        RefreshAll();
    }

    private static int ParseQty()
    {
        int v;
        if (!int.TryParse((qtyText ?? "").Trim(), out v) || v <= 0)
        {
            SetMsg(StockExchangeEntry.L("msg_bad_qty"), DownColor);
            return 0;
        }
        return Mathf.Min(v, 999999);
    }

    private static void SetMsg(string text, Color c)
    {
        if (msgEl == null)
        {
            return;
        }
        msgEl.SetText(text);
        msgEl.SetTextColor(c);
    }

    // ---------------------------------------------------------------- 工具

    private static void RebuildLayout()
    {
        if (detailHost != null)
        {
            CustomUIBuilder.RebuildLayout(detailHost);
        }
    }

    private static string PctText(float chg)
    {
        string s = (chg * 100f).ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + "%";
        return s;
    }

    private static string Signed(int v)
    {
        return v > 0 ? "+" + v.ToString("N0") : v.ToString("N0");
    }

    private static Color SignColor(int v)
    {
        if (v > 0) return StockExchangeEntry.Instance != null && StockExchangeEntry.Instance.CnColors() ? DownColor : UpColor;
        if (v < 0) return StockExchangeEntry.Instance != null && StockExchangeEntry.Instance.CnColors() ? UpColor : DownColor;
        return FlatColor;
    }

    private static Color SignColorRaw(float chg)
    {
        if (chg > 0.0005f) return SignColor(1);
        if (chg < -0.0005f) return SignColor(-1);
        return FlatColor;
    }
}
