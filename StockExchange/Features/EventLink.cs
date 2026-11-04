using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// 事件联动：原版 StoreEvent → 个股冲击映射（核心卖点）
//   - 激活中的事件 → 当日结算冲击（立即生效）
//   - 传闻/未来事件（IsKnown）→ 只做"预警提示"，不提前异动（避免看K线猜传闻取代读报）
//   - 数据驱动映射表：原版事件改版/新增时只需补表
// ============================================================================

public static class EventLink
{
    // (事件 identifier, 股票代码, 冲击 %) —— identifier 与 StoreEventManager 蓝图表一致
    private static readonly string[,] Map =
    {
        // ---- 正常事件（normalEventBlueprints）----
        { "seedShortage",                    "NFR", "15" },
        { "badNutrifruitHarvest",            "NFR", "-12" },
        { "miningExpeditionReturn",          "MIN", "15" },
        { "miner_return_ice_asteroid",       "MIN", "12" },
        { "storeroom_excavated",             "CON", "10" },
        { "construction_project",            "CON", "10" },
        { "cruiseShipDocked",                "CRZ", "12" },
        { "cruiseShipDocked",                "ART", "6"  },
        { "water_treatment_breakdown",       "HYD", "8"  },
        { "scheduled_treatment_maintenance", "HYD", "-2" },
        { "stationWideBlackout",             "PWR", "14" },
        { "stationWideBlackout",             "ART", "-8" },
        { "powerShortage",                   "PWR", "6"  },
        { "powerPlantMaintenance",           "PWR", "-3" },
        { "medicalSuppliesShortage",         "MED", "12" },
        { "oxymoreWave",                     "MED", "-5" },
        { "oxymoreWave",                     "ART", "-5" },
        { "lower_level_mob_justice",         "ART", "-6" },
        { "lower_level_mob_justice",         "MIN", "-4" },

        // ---- 装饰事件（cosmeticEventBlueprints）----
        { "NutrifruitGrow",              "NFR", "5"  },
        { "CommunityGardenExpansion",    "NFR", "4"  },
        { "ScheduledEletricalMaintenance","HYD", "-2" },
        { "WaterFountainUnveiled",       "HYD", "4"  },
        { "RecyclingAwarenessCampaign",  "HYD", "2"  },
        { "MaintenanceCrewStrike",       "CON", "-6" },
        { "MaintenanceCrewStrike",       "HYD", "-4" },
        { "TechDemoDay",                 "CON", "4"  },
        { "TechDemoDay",                 "PWR", "4"  },
        { "NewTransitLine",              "CON", "8"  },
        { "NewTransitLine",              "CRZ", "4"  },
        { "ArtistShowcase",              "ART", "12" },
        { "ArtContestHeld",              "ART", "10" },
        { "CulturalCelebration",         "ART", "8"  },
        { "CulturalCelebration",         "CRZ", "4"  },
    };

    /// 激活事件对该股票的总冲击（小数，如 +0.15）。
    public static float GetShock(string stockCode, List<StoreEvent> activeEvents)
    {
        if (activeEvents == null || activeEvents.Count == 0)
        {
            return 0f;
        }
        float sum = 0f;
        for (int r = 0; r < Map.GetLength(0); r++)
        {
            if (Map[r, 1] != stockCode)
            {
                continue;
            }
            string evId = Map[r, 0];
            for (int e = 0; e < activeEvents.Count; e++)
            {
                if (activeEvents[e] != null && activeEvents[e].identifier == evId)
                {
                    sum += ParsePct(Map[r, 2]);
                }
            }
        }
        return sum / 100f;
    }

    /// 已知未来事件（传闻）是否会在近 inDaysDays 天内冲击该股票（只提示，不剧透方向）。
    public static bool HasFutureWarning(string stockCode, int inDaysDays)
    {
        try
        {
            var mgr = StoreStation.Instance != null ? StoreStation.Instance.storeEventManager : null;
            if (mgr == null || mgr.futurEvents == null || mgr.futurEventsDay == null)
            {
                return false;
            }
            int n = Mathf.Min(mgr.futurEvents.Count, mgr.futurEventsDay.Count);
            for (int i = 0; i < n; i++)
            {
                StoreEvent ev = mgr.futurEvents[i];
                if (ev == null || !ev.IsKnown || mgr.futurEventsDay[i] > inDaysDays)
                {
                    continue;
                }
                for (int r = 0; r < Map.GetLength(0); r++)
                {
                    if (Map[r, 1] == stockCode && Map[r, 0] == ev.identifier)
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
        }
        return false;
    }

    private static float ParsePct(string s)
    {
        float v;
        if (float.TryParse(s, out v))
        {
            return v;
        }
        return 0f;
    }
}
