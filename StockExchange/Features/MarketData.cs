using System;
using UnityEngine;

// ============================================================================
// 市场数据：股票目录 + 全局状态 + 存档持久化 + 每日结算调度
//   - 状态单 key JSON 走 ModHelper.SetModData（随玩家存档 ES3 保存）
//   - 随机数：独立 System.Random（种子入档），绝不触碰游戏全局 RNG
//   - 结算幂等：lastSettledDay 防重（读档/重进不补算、不重复结算）
// ============================================================================

public class StockDef
{
    public string code;      // 行情代码，如 HYD
    public string sector;    // 板块 id（板块联动用）
    public float basePrice;  // 基准价
    public float volatility; // 基础日波动 σ
    public int minTier;      // 交易所需账户等级（1 接入 / 2 量化终端）
}

public static class MarketCatalog
{
    public const int Count = 8;

    // 首发标的：4 只 T1 稳健（HYD/NFR/CON/MED）+ 4 只 T2（MIN/CRZ/PWR/ART）
    public static readonly StockDef[] Stocks =
    {
        new StockDef { code = "HYD", sector = "utility",    basePrice = 65f,  volatility = 0.020f, minTier = 1 },
        new StockDef { code = "NFR", sector = "farm",       basePrice = 40f,  volatility = 0.025f, minTier = 1 },
        new StockDef { code = "CON", sector = "build",      basePrice = 30f,  volatility = 0.035f, minTier = 1 },
        new StockDef { code = "MED", sector = "medical",    basePrice = 200f, volatility = 0.030f, minTier = 1 },
        new StockDef { code = "MIN", sector = "mining",     basePrice = 85f,  volatility = 0.045f, minTier = 2 },
        new StockDef { code = "CRZ", sector = "travel",     basePrice = 55f,  volatility = 0.040f, minTier = 2 },
        new StockDef { code = "PWR", sector = "energy",     basePrice = 120f, volatility = 0.030f, minTier = 2 },
        new StockDef { code = "ART", sector = "luxury",     basePrice = 15f,  volatility = 0.070f, minTier = 2 },
    };
}

[Serializable]
public class ExchangeState
{
    public int version = 1;
    public int seed;                                   // 独立随机源种子
    public int lastSettledDay;                         // 幂等结算水位
    public float[] prices = new float[MarketCatalog.Count];
    public float[] prevClose = new float[MarketCatalog.Count];
    public float[] historyFlat = new float[MarketCatalog.Count * MarketData.HistLen]; // 旧→新
    public int[] qty = new int[MarketCatalog.Count];
    public float[] avgCost = new float[MarketCatalog.Count];
    public int realized;                               // 已实现盈亏（含费）
    public int trades;
}

public static class MarketData
{
    public const int HistLen = 30;
    public const int MaxSettleSteps = 40; // 断档保护：一次最多补 40 天
    private const string DataKey = "stocks_state_v1";

    public static ExchangeState State { get; private set; }

    public static bool IsReady { get { return State != null; } }

    // ------------------------------------------------------------ 生命周期

    /// 读档 / 会话内兜底：确保有状态且结算到指定日。返回是否发生了结算。
    public static bool EnsureAndSettle(int day)
    {
        if (!InGame())
        {
            return false;
        }
        if (State == null && !Restore())
        {
            Seed(day);
        }
        return SettleTo(day);
    }

    /// 每次读档前调用：先清内存态，再从【本存档】的 ModData 恢复。
    /// 防止从存档 A 退到菜单再读存档 B 时，内存里残留 A 的行情/持仓串档。
    public static void ReloadForGameLoad(int day)
    {
        State = null;
        EnsureAndSettle(day);
    }

    /// 新游戏：清空重建账户。
    public static void ResetForNewGame(int day)
    {
        State = null;
        try
        {
            ModHelper.RemoveModData(StockExchangeEntry.ModId, DataKey);
        }
        catch (Exception)
        {
        }
        if (InGame())
        {
            Seed(day);
        }
    }

    public static void OnTornDown()
    {
        State = null;
    }

    private static bool InGame()
    {
        try
        {
            return StoreStation.Instance != null
                && StoreStation.Instance.storeEventManager != null;
        }
        catch
        {
            return false;
        }
    }

    // ------------------------------------------------------------ 恢复 / 初始化

    private static bool Restore()
    {
        try
        {
            string raw = ModHelper.GetModData(StockExchangeEntry.ModId, DataKey, null);
            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }
            ExchangeState s = JsonUtility.FromJson<ExchangeState>(raw);
            if (s == null || s.prices == null || s.prices.Length != MarketCatalog.Count
                || s.historyFlat == null || s.historyFlat.Length != MarketCatalog.Count * HistLen)
            {
                Debug.LogWarning("[OrbitExchange] 存档状态结构不匹配，重建账户");
                return false;
            }
            State = s;
            PriceEngine.Reseed(s.seed);
            Debug.Log($"[OrbitExchange] 状态已恢复: day={s.lastSettledDay} trades={s.trades} realized={s.realized}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[OrbitExchange] 状态恢复失败: " + e.Message);
            return false;
        }
    }

    /// 全新账户：合成 30 日历史（围绕基准价的温和随机游走），K 线不空窗。
    private static void Seed(int day)
    {
        int seed = Guid.NewGuid().GetHashCode() & 0x7fffffff;
        System.Random rng = new System.Random(seed);
        ExchangeState s = new ExchangeState();
        s.seed = seed;
        s.lastSettledDay = Mathf.Max(1, day);

        for (int i = 0; i < MarketCatalog.Count; i++)
        {
            StockDef def = MarketCatalog.Stocks[i];
            float p = def.basePrice;
            for (int k = 0; k < HistLen; k++)
            {
                double gauss = PriceEngine.GaussianFrom(rng);
                float ret = Mathf.Clamp((float)gauss * def.volatility * 0.6f, -0.08f, 0.08f);
                p = Mathf.Max(1f, Mathf.Round(p * (1f + ret) * 10f) / 10f);
                s.historyFlat[i * HistLen + k] = p;
            }
            s.prices[i] = s.historyFlat[i * HistLen + HistLen - 1];
            s.prevClose[i] = s.historyFlat[i * HistLen + HistLen - 2];
        }

        State = s;
        PriceEngine.Reseed(seed);
        Persist();
        Debug.Log($"[OrbitExchange] 全新账户已初始化 (day={s.lastSettledDay}, seed={seed})");
    }

    public static void Persist()
    {
        if (State == null)
        {
            return;
        }
        try
        {
            ModHelper.SetModData(StockExchangeEntry.ModId, DataKey, JsonUtility.ToJson(State));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[OrbitExchange] Persist 失败: " + e.Message);
        }
    }

    // ------------------------------------------------------------ 每日结算

    /// 逐日推进到 day（幂等：lastSettledDay 水位）。事件冲击取结算时刻的激活事件。
    public static bool SettleTo(int day)
    {
        if (State == null || day <= State.lastSettledDay || !InGame())
        {
            return false;
        }
        var e = StockExchangeEntry.Instance;
        float drift = e != null ? e.Drift() : 0.0008f;
        float volMult = e != null ? e.VolMult() : 1f;

        int guard = 0;
        while (State.lastSettledDay < day && guard++ < MaxSettleSteps)
        {
            System.Collections.Generic.List<StoreEvent> evs =
                StoreStation.Instance.storeEventManager.GetActiveEvents();

            State.prevClose = (float[])State.prices.Clone();

            for (int i = 0; i < MarketCatalog.Count; i++)
            {
                StockDef def = MarketCatalog.Stocks[i];
                float shock = EventLink.GetShock(def.code, evs);
                float ma = MovingAverage(i);
                float np = PriceEngine.Step(def, State.prices[i], ma, shock, drift, volMult);
                State.prices[i] = np;
                PushHistory(i, np);
            }
            State.lastSettledDay++;
        }

        if (State.lastSettledDay < day)
        {
            // 断档保护：宁可跳过不可重算（防读档刷价格）
            Debug.LogWarning($"[OrbitExchange] 行情断档 {day - State.lastSettledDay} 天，跳过补算");
            State.lastSettledDay = day;
        }

        Persist();
        return true;
    }

    private static void PushHistory(int i, float price)
    {
        int baseIdx = i * HistLen;
        Array.Copy(State.historyFlat, baseIdx + 1, State.historyFlat, baseIdx, HistLen - 1);
        State.historyFlat[baseIdx + HistLen - 1] = price;
    }

    /// 30 日简单均线（含当日）。
    public static float MovingAverage(int i)
    {
        int baseIdx = i * HistLen;
        double sum = 0.0;
        for (int k = 0; k < HistLen; k++)
        {
            sum += State.historyFlat[baseIdx + k];
        }
        return (float)(sum / HistLen);
    }

    /// 历史切片（旧→新，长度 HistLen）。
    public static float[] History(int i)
    {
        float[] outp = new float[HistLen];
        Array.Copy(State.historyFlat, i * HistLen, outp, 0, HistLen);
        return outp;
    }
}
