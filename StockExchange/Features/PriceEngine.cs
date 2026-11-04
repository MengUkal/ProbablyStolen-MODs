using System;
using UnityEngine;

// ============================================================================
// 定价引擎
//   收盘价 = 前收 × (1 + drift + σ·gauss + 事件冲击 + 均值回归)，clamp ±25%
//   - 独立 System.Random（种子入档），绝不消耗游戏全局 RNG 流
//   - 均值回归：偏离 30 日均线每 10% 回拉 1%（防单边漂移上天/归零）
// ============================================================================

public static class PriceEngine
{
    private const float DailyLimit = 0.25f; // 涨跌停
    private const float MinPrice = 1f;
    private const float ReversionCoef = 0.10f;

    private static System.Random rng;
    private static double spare;
    private static bool hasSpare;

    public static void Reseed(int seed)
    {
        rng = new System.Random(seed);
        hasSpare = false;
        spare = 0.0;
    }

    /// Box-Muller 高斯随机数。
    public static double NextGaussian()
    {
        if (rng == null)
        {
            Reseed(12345);
        }
        if (hasSpare)
        {
            hasSpare = false;
            return spare;
        }
        double u1 = 1.0 - rng.NextDouble(); // 避开 0
        double u2 = rng.NextDouble();
        double r = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        spare = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        hasSpare = true;
        return r;
    }

    /// 供 Seed() 用的独立高斯（不占用引擎内部缓存）。
    public static double GaussianFrom(System.Random source)
    {
        double u1 = 1.0 - source.NextDouble();
        double u2 = source.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    /// 计算一只股票的下一个收盘价。
    public static float Step(StockDef def, float price, float ma30, float eventShock,
        float drift, float volMult)
    {
        float sigma = def.volatility * Mathf.Max(0f, volMult);
        float gauss = (float)NextGaussian();

        float reversion = 0f;
        if (ma30 > 0.01f)
        {
            reversion = ((ma30 - price) / ma30) * ReversionCoef;
        }

        float ret = drift + sigma * gauss + eventShock + reversion;
        ret = Mathf.Clamp(ret, -DailyLimit, DailyLimit);

        float np = price * (1f + ret);
        return Mathf.Max(MinPrice, Mathf.Round(np * 10f) / 10f);
    }
}
