using System;
using UnityEngine;

// ============================================================================
// "已见过"物品集合（图鉴的范围过滤用）。买入/卖出/托运过夜时标记。
// ============================================================================

public static class CodexSeen
{
    private const string DataKey = "codex_seen";
    private const int Cap = 5000;

    private static readonly System.Collections.Generic.HashSet<string> seen = new System.Collections.Generic.HashSet<string>();

    public static bool Has(string id)
    {
        return id != null && seen.Contains(id);
    }

    public static void Add(string id)
    {
        if (string.IsNullOrEmpty(id) || seen.Contains(id))
        {
            return;
        }
        if (seen.Count >= Cap)
        {
            return; // 防止存档膨胀
        }
        seen.Add(id);
    }

    public static void Restore()
    {
        seen.Clear();
        if (TryReadInto(ItemCodexEntry.ModId, DataKey, seen))
        {
            return;
        }
        // 一次性迁移：旧 qol.pack（已解散的合体 MOD）命名空间里的"已见过"集合
        // 否则老玩家存档里的数据会静默丢失（图鉴"范围：已见过"会变空）
        if (TryReadInto("qol.pack", "qol_seen", seen))
        {
            Persist();
            Debug.Log("[ItemCodex] 已从 qol.pack 迁移已见过物品数据");
        }
    }

    private static bool TryReadInto(string modId, string key, System.Collections.Generic.HashSet<string> target)
    {
        try
        {
            string raw = ModHelper.GetModData(modId, key, null);
            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }
            bool any = false;
            foreach (string id in raw.Split(';'))
            {
                if (!string.IsNullOrEmpty(id))
                {
                    target.Add(id);
                    any = true;
                }
            }
            return any;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ItemCodex] CodexSeen read '" + modId + "/" + key + "' failed: " + e.Message);
            return false;
        }
    }

    public static void Persist()
    {
        try
        {
            ModHelper.SetModData(ItemCodexEntry.ModId, DataKey, string.Join(";", seen));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ItemCodex] CodexSeen.Persist failed: " + e.Message);
        }
    }

    public static void Reset()
    {
        seen.Clear();
        try
        {
            ModHelper.RemoveModData(ItemCodexEntry.ModId, DataKey);
        }
        catch (Exception)
        {
        }
    }

    public static void OnTornDown()
    {
        seen.Clear();
    }
}
