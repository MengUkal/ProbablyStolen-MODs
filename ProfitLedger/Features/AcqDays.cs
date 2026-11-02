using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// 持有天数记录：uniqueId -> 获得日（StoreStation.GetDayCounter()）
// 通过 ModData 持久化（随玩家存档保存/读取）。物品卖出后夜间自动清理孤儿条目。
// ============================================================================

public static class AcqDays
{
    private const string DataKey = "ledger_acq_days";

    private static readonly Dictionary<int, int> map = new Dictionary<int, int>();

    public static void Record(int uniqueId, int day)
    {
        if (uniqueId != 0 && day >= 0)
        {
            map[uniqueId] = day;
        }
    }

    /// 返回已持有天数；未知物品返回 -1。
    public static int GetDays(int uniqueId)
    {
        if (uniqueId == 0 || !map.TryGetValue(uniqueId, out int day))
        {
            return -1;
        }
        int today;
        try
        {
            today = StoreStation.GetDayCounter();
        }
        catch (Exception)
        {
            return -1;
        }
        return Mathf.Max(0, today - day);
    }

    public static void Restore()
    {
        map.Clear();
        if (TryReadInto(ProfitLedgerEntry.ModId, DataKey, map))
        {
            return;
        }
        // 一次性迁移：旧 qol.pack（已解散的合体 MOD）命名空间里的持有天数表
        // 否则老玩家的存档数据会静默丢失（编译不报错、运行时不报错）
        if (TryReadInto("qol.pack", "qol_acq_days", map))
        {
            Persist();
            Debug.Log("[ProfitLedger] 已从 qol.pack 迁移持有天数数据");
        }
    }

    private static bool TryReadInto(string modId, string key, Dictionary<int, int> target)
    {
        try
        {
            string raw = ModHelper.GetModData(modId, key, null);
            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }
            bool any = false;
            foreach (string pair in raw.Split(';'))
            {
                int sep = pair.IndexOf(':');
                if (sep <= 0)
                {
                    continue;
                }
                if (int.TryParse(pair.Substring(0, sep), out int id) &&
                    int.TryParse(pair.Substring(sep + 1), out int day))
                {
                    target[id] = day;
                    any = true;
                }
            }
            return any;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ProfitLedger] AcqDays read '" + modId + "/" + key + "' failed: " + e.Message);
            return false;
        }
    }

    /// 夜间调用：清理已不存在的物品条目并写回 ModData。
    public static void PruneAndPersist()
    {
        try
        {
            if (PlayerStore.HasInstance)
            {
                List<GameItem> alive = PlayerStore.Instance.FindAllItem();
                HashSet<int> aliveIds = new HashSet<int>();
                foreach (GameItem it in alive)
                {
                    if (it != null)
                    {
                        aliveIds.Add(it.GetUniqueID());
                    }
                }
                List<int> dead = new List<int>();
                foreach (KeyValuePair<int, int> kv in map)
                {
                    if (!aliveIds.Contains(kv.Key))
                    {
                        dead.Add(kv.Key);
                    }
                }
                for (int i = 0; i < dead.Count; i++)
                {
                    map.Remove(dead[i]);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ProfitLedger] AcqDays.Prune failed: " + e.Message);
        }
        Persist();
    }

    public static void Persist()
    {
        try
        {
            var sb = new System.Text.StringBuilder("v1");
            foreach (KeyValuePair<int, int> kv in map)
            {
                sb.Append(';').Append(kv.Key).Append(':').Append(kv.Value);
            }
            ModHelper.SetModData(ProfitLedgerEntry.ModId, DataKey, sb.ToString());
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ProfitLedger] AcqDays.Persist failed: " + e.Message);
        }
    }

    public static void Reset()
    {
        map.Clear();
        try
        {
            ModHelper.RemoveModData(ProfitLedgerEntry.ModId, DataKey);
        }
        catch (Exception)
        {
        }
    }

    public static void OnTornDown()
    {
        map.Clear();
    }
}
