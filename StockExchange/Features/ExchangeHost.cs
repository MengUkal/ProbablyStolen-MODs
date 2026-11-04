using System;
using UnityEngine;

// ============================================================================
// 常驻宿主：快捷键轮询 + 日切结算轮询（幂等，兜底一切挂钩时序）
// ============================================================================

public class ExchangeHost : MonoBehaviour
{
    private static ExchangeHost instance;

    public static ExchangeHost Create()
    {
        if (instance != null)
        {
            return instance;
        }
        GameObject go = new GameObject("OrbitExchange_Host");
        UnityEngine.Object.DontDestroyOnLoad(go);
        instance = go.AddComponent<ExchangeHost>();
        return instance;
    }

    public static void TearDown()
    {
        if (instance != null)
        {
            UnityEngine.Object.Destroy(instance.gameObject);
            instance = null;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        StockExchangeEntry entry = StockExchangeEntry.Instance;
        if (entry == null)
        {
            return;
        }

        // ---- 场景哨兵：只在游戏场景内活动 ----
        // StoreUIManager 是 Awake 赋值的普通场景单例：主菜单期为 null 且访问无副作用。
        // 绝不能在菜单期触碰 StoreStation.Instance / PlayerStore.Instance（惰性构造单例，
        // 会 NRE 或污染游戏自身的局内判断）。
        if (StoreUIManager.Instance == null)
        {
            return;
        }

        // ---- 日切结算（幂等）----
        try
        {
            bool settled = MarketData.EnsureAndSettle(StockExchangeEntry.GetDay());
            if (settled && ExchangeUI.IsVisible)
            {
                ExchangeUI.RefreshAll();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[OrbitExchange] 结算轮询失败: " + e.Message);
        }

        // ---- 报纸收盘快报（每日一刊，缺刊即补；内部自带日切判重）----
        try
        {
            OrbitNews.TickDigest();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[OrbitExchange] 收盘快报刊印失败: " + e.Message);
        }

        // ---- 快捷键（仅游戏场景内响应）----
        if (entry.exchangeKey != KeyCode.None && Input.GetKeyDown(entry.exchangeKey))
        {
            ExchangeUI.Toggle();
        }
    }
}
