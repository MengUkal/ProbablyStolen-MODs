using System;
using System.Text;
using UnityEngine;

// ============================================================================
// 报纸市场版（P3-1 最小实验）——"读报炒股"卖点闭环
//   把「环带交易所收盘快报」注册成自定义 StoreEvent 印进游戏报纸。
//   反编译结论（可行性好于交接文档预期）：
//   - StoreEventManager.normalEventBlueprints 是 public static List<StoreEventBlueprint>，
//     直接 RemoveAll+Add 幂等注册，【零反射】（原文档假设要反射 append，实测公开 API 即可）
//   - blueprint.storeEventFunc 在游戏抽取/激活时才求值 → 描述实时生成最新收盘摘要
//   - 同 identifier 事件被游戏去重/续期（ActivateEvent→ExtendActiveEvent）
//   - identifier 不在 EventLink 冲击映射表 → 对股价冲击恒为 0，不干扰定价
//   - action id 查表均为 TryGetValue + value?.Invoke()，自定义 id 查不到时安全跳过
//   - 报纸渲染只读 newsName / newsDescription / duration（NewsUIElement.PopulateUI）
//   机制：宿主 Update 轮询"缺刊即补"——日切后报纸没有快报就补印今天一期（force），
//   读档后先核对在版状态防重复（force 会跳过游戏自身的去重，必须自查）。
// ============================================================================

public static class OrbitNews
{
    public const string Identifier = "orbitExchangeDaily";
    public const int Odd = 10; // normalEventBlueprints 池内权重（原版普通事件 4~15）

    private static int lastSeenDay = -1;
    private static bool digestUp;

    // ------------------------------------------------------------ 蓝图注册

    /// OnEnable 调用（主菜单期安全：只操作 static 列表，不触碰任何场景单例）。
    public static void RegisterBlueprint()
    {
        try
        {
            var pool = StoreEventManager.normalEventBlueprints;
            if (pool == null)
            {
                Debug.LogWarning("[OrbitExchange] normalEventBlueprints 为 null，快报未注册");
                return;
            }
            pool.RemoveAll(b => b != null && b.identifier == Identifier);
            pool.Add(new StoreEventBlueprint(CreateDailyDigest, Odd, Identifier));
            Debug.Log($"[OrbitExchange] 报纸市场版已注册（normalEventBlueprints={pool.Count}）");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[OrbitExchange] 报纸市场版注册失败: " + e.Message);
        }
    }

    /// OnDisable 调用。已印出的存档事件是纯数据，无 MOD 也能自然到期，无害。
    public static void UnregisterBlueprint()
    {
        try
        {
            StoreEventManager.normalEventBlueprints?.RemoveAll(b => b != null && b.identifier == Identifier);
        }
        catch (Exception)
        {
        }
    }

    /// 读档/新游戏后重置轮询状态（下次 Tick 重新核对在版）。
    public static void ResetForGameLoad()
    {
        lastSeenDay = -1;
        digestUp = false;
    }

    // ------------------------------------------------------------ 每日刊印

    /// 宿主每帧调用：日切核对一次在版，缺刊补印（force 跳过冷却/兼容检查）。
    public static void TickDigest()
    {
        StockExchangeEntry entry = StockExchangeEntry.Instance;
        if (entry == null || !entry.NewsDailyOn() || !Broker.InGame())
        {
            return;
        }
        int day = StockExchangeEntry.GetDay();
        if (day != lastSeenDay)
        {
            lastSeenDay = day;
            digestUp = IsDigestActive(); // 读档场景：存档里已有本刊则不重发
        }
        if (digestUp)
        {
            return;
        }
        StoreEventManager mgr = StoreStation.Instance != null ? StoreStation.Instance.storeEventManager : null;
        if (mgr == null)
        {
            return;
        }
        mgr.ActivateEvent(CreateDailyDigest(), false, true);
        digestUp = true;
        Debug.Log($"[OrbitExchange] 收盘快报已送印 (day={day})");
    }

    /// 调试面板用：立即强推一期（不判重，用游戏侧 identifier 续期兜底）。
    public static void ActivateDigestNow()
    {
        if (!Broker.InGame())
        {
            return;
        }
        StoreEventManager mgr = StoreStation.Instance != null ? StoreStation.Instance.storeEventManager : null;
        if (mgr == null)
        {
            return;
        }
        mgr.ActivateEvent(CreateDailyDigest(), false, true);
        digestUp = true;
        Debug.Log("[OrbitExchange] 收盘快报已强推（调试）");
    }

    private static bool IsDigestActive()
    {
        try
        {
            StoreEventManager mgr = StoreStation.Instance != null ? StoreStation.Instance.storeEventManager : null;
            if (mgr == null)
            {
                return true; // 拿不到管理器就当在版，避免误发
            }
            var evs = mgr.GetActiveEvents();
            for (int i = 0; i < evs.Count; i++)
            {
                if (evs[i] != null && evs[i].identifier == Identifier)
                {
                    return true;
                }
            }
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    // ------------------------------------------------------------ 事件工厂

    /// 工厂在游戏抽取/激活时求值：描述始终基于最新收盘数据生成。
    public static StoreEvent CreateDailyDigest()
    {
        StoreEvent e = new StoreEvent();
        e.identifier = Identifier;
        e.newsName = StockExchangeEntry.L("news_title");
        e.displayName = StockExchangeEntry.L("news_display");
        e.newsDescription = BuildDigest();
        e.duration = 1;
        e.cooldown = 0;
        e.importance = 3;
        e.InitNormalEvent();
        return e;
    }

    /// 一股票一行（名称 价格▲涨跌%），共 8 行 + 提示行。
    /// 报纸窄栏约 22 个半角单位宽：中文名 4~5 字 + 行情恰好不折行；
    /// 挤多只会被 TMP 在任意处折行把一只股票撕成两半（实测教训）。
    private static string BuildDigest()
    {
        if (!MarketData.IsReady)
        {
            return StockExchangeEntry.L("news_desc_offline");
        }
        var st = MarketData.State;
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < MarketCatalog.Count; i++)
        {
            float p = st.prices[i];
            float pc = Mathf.Max(0.01f, st.prevClose[i]);
            float chg = (p - pc) / pc * 100f;
            string arrow = chg > 0.05f ? "▲" : (chg < -0.05f ? "▼" : "·");
            sb.Append(StockExchangeEntry.L("st_" + MarketCatalog.Stocks[i].code + "_name"))
                .Append(' ').Append(p.ToString("0.0")).Append(arrow)
                .Append(chg.ToString("+0.0;-0.0;0.0")).Append('%')
                .Append('\n');
        }
        sb.Append(StockExchangeEntry.L("news_desc_hint"));
        return sb.ToString();
    }
}
