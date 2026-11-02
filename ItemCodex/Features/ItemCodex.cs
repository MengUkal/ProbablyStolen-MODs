using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// 物品图鉴 —— 基于游戏官方 CustomUI 框架的全物品浏览窗口
//   - 数据源：DirectoryMaster 中所有 ItemDirectory 的物品工厂
//     （临时实例化读取名称/基准价/描述后立即销毁，分帧构建避免卡顿）
//   - 搜索（防抖）/ 分类下拉（按 ItemDirectory 分组）/ "已见过"范围过滤
//   - 点击行显示详情（图标/描述/风格文本）
// 打开方式：设置页快捷键（默认 F9）或调试面板按钮
// ============================================================================

public static class ItemCodex
{
    private class Row
    {
        public string id;
        public string dir;
        public string name;
        public long baseValue;
        public string spriteKey;
        public string spriteAtlas;
        public bool useModSprite;
        public string desc;
        public string longDesc;
        public string flavor;
        public string lower;
        public GameObject go;
    }

    private static CustomUIWindow window;
    private static GameObject windowRoot;
    private static RectTransform listContent;
    private static RectTransform detailHost;
    private static CustomUIElement countElement;

    private static readonly List<Row> rows = new List<Row>();
    private static readonly Dictionary<string, Row> byId = new Dictionary<string, Row>();
    private static readonly List<string> dirIds = new List<string>();

    private static string filterText = "";
    private static int dirIndex = 0;      // 0 = 全部分类
    private static bool seenOnly = false;
    private static bool building;
    private static bool dataBuilt;

    // ---------------------------------------------------------------- 开关
    public static void Toggle()
    {
        if (window != null && windowRoot != null)
        {
            if (windowRoot.activeSelf)
            {
                window.Hide();
            }
            else
            {
                window.Show();
            }
            return;
        }
        Build();
    }

    /// 场景重建后窗口可能已被销毁；清空引用以便下次重新构建。
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
        countElement = null;
        building = false;
        foreach (Row r in rows)
        {
            r.go = null;
        }
    }

    // ---------------------------------------------------------------- 构建
    private static void Build()
    {
        if (building)
        {
            return;
        }
        if (CustomUIManager.Instance == null)
        {
            Debug.Log("[ItemCodex] Codex unavailable: CustomUIManager not present");
            return;
        }

        try
        {
            CustomUIBuilder b = CustomUIManager.Window("qol_codex", ItemCodexEntry.L("codex_title"));
            b.SetSize(860f, 620f).Center().SetCloseOnEscape().SetDraggable(true);

            // ---- 顶部控件行 ----
            b.BeginRow();
            b.AddInput(placeholder: ItemCodexEntry.L("codex_search"), initial: filterText,
                onTyping: s => RequestFilter(s, false));
            List<string> dirOptions = BuildDirOptions();
            b.AddDropdown(dirOptions, Mathf.Clamp(dirIndex, 0, dirOptions.Count - 1),
                i => { dirIndex = i; RequestFilter(null, true); }, "codex_dir");
            b.AddDropdown(new List<string>
                {
                    ItemCodexEntry.L("codex_scope_all"),
                    ItemCodexEntry.L("codex_scope_seen")
                },
                seenOnly ? 1 : 0,
                i => { seenOnly = (i == 1); RequestFilter(null, true); }, "codex_scope");
            b.End();

            b.AddLabel("", "codex_count");
            countElement = b.Last;

            // ---- 列表区（记录容器，便于过滤/重建行）----
            b.BeginScrollFlexible();
            listContent = b.CurrentContainer;
            b.End();

            // ---- 详情区 ----
            b.BeginPanel(-1f, 150f, TextAnchor.UpperLeft, 8f, 4f, "codex_detail");
            detailHost = b.CurrentContainer;
            b.End();

            window = b.Show();
            windowRoot = (window.root != null) ? window.root.gameObject : null;
            window.onClose = () => { /* 仅隐藏，窗口对象保留以便秒开 */ };

            foreach (Row r in rows)
            {
                r.go = null; // 旧的视觉行全部失效
            }

            if (!dataBuilt)
            {
                building = true;
                if (CodexHost.Instance != null)
                {
                    CodexHost.Instance.StartCoroutine(BuildData());
                }
                else
                {
                    building = false;
                }
            }
            else
            {
                foreach (Row r in rows)
                {
                    if (Matches(r))
                    {
                        AddRowVisual(r);
                    }
                }
                UpdateCount();
                CustomUIBuilder.RebuildLayout(listContent);
            }
            UpdateCount();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ItemCodex] Codex build failed: " + e.Message);
            OnTornDown();
        }
    }

    private static void RequestFilter(string text, bool immediate)
    {
        if (CodexHost.Instance != null)
        {
            CodexHost.Instance.SetFilterDebounce(text, immediate);
        }
    }

    private static List<string> BuildDirOptions()
    {
        List<string> options = new List<string>();
        dirIds.Clear();
        options.Add(ItemCodexEntry.L("codex_dir_all"));
        dirIds.Add("ALL");
        try
        {
            List<string> names = DirectoryMaster.GetDictionaryClassNameList<GameItem>();
            if (names != null)
            {
                List<string> sorted = new List<string>(names);
                sorted.Sort(StringComparer.Ordinal);
                foreach (string n in sorted)
                {
                    dirIds.Add(n);
                    options.Add(LocDir(PrettyDir(n)));
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ItemCodex] Codex dir list failed: " + e.Message);
        }
        return options;
    }

    /// 目录名本地化：查 codex_dir_<名>，查不到（GetLocalized 会原样返回 key）则回退英文类名。
    private static string LocDir(string pretty)
    {
        string key = "codex_dir_" + pretty;
        string loc = ModHelper.GetLocalized(ItemCodexEntry.ModId, key);
        return (loc == key) ? pretty : loc;
    }

    private static string PrettyDir(string n)
    {
        if (n.EndsWith("ItemDirectory", StringComparison.Ordinal))
        {
            return n.Substring(0, n.Length - "ItemDirectory".Length);
        }
        if (n.EndsWith("Directory", StringComparison.Ordinal))
        {
            return n.Substring(0, n.Length - "Directory".Length);
        }
        return n;
    }

    // ---------------------------------------------------------------- 数据
    private static IEnumerator BuildData()
    {
        List<KeyValuePair<string, string>> targets = new List<KeyValuePair<string, string>>(); // (dir, id)
        try
        {
            List<string> names = DirectoryMaster.GetDictionaryClassNameList<GameItem>();
            if (names != null)
            {
                foreach (string n in names)
                {
                    List<string> ids = DirectoryMaster.GetIdentifierList<GameItem>(n);
                    if (ids == null)
                    {
                        continue;
                    }
                    foreach (string id in ids)
                    {
                        if (!string.IsNullOrEmpty(id) && !byId.ContainsKey(id))
                        {
                            targets.Add(new KeyValuePair<string, string>(n, id));
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ItemCodex] Codex enumerate failed: " + e.Message);
        }
        targets.Sort((a, b2) => string.CompareOrdinal(a.Value, b2.Value));

        int budget = 0;
        foreach (KeyValuePair<string, string> kv in targets)
        {
            if (byId.ContainsKey(kv.Value))
            {
                continue;
            }
            Row r = CreateRowData(kv.Value, kv.Key);
            if (r != null)
            {
                rows.Add(r);
                byId[r.id] = r;
                if (Matches(r))
                {
                    AddRowVisual(r);
                }
            }
            if (++budget >= 25)
            {
                budget = 0;
                UpdateCount();
                CustomUIBuilder.RebuildLayout(listContent);
                yield return null;
            }
        }
        building = false;
        dataBuilt = true;
        UpdateCount();
        CustomUIBuilder.RebuildLayout(listContent);
    }

    private static Row CreateRowData(string id, string dir)
    {
        try
        {
            Func<GameItem> factory = DirectoryMaster.GetFactory<GameItem>(id);
            if (factory == null)
            {
                return null;
            }
            GameItem it = factory();
            if (it == null)
            {
                return null;
            }

            Row r = new Row();
            r.id = id;
            r.dir = dir;
            r.spriteKey = it.spritePath;
            r.spriteAtlas = it.spriteAtlasPath;
            r.useModSprite = it.useSpriteFromMod;
            try
            {
                r.name = it.GetDisplayName();
            }
            catch
            {
                r.name = null;
            }
            if (string.IsNullOrEmpty(r.name))
            {
                r.name = string.IsNullOrEmpty(it.identifierName) ? id : it.identifierName;
            }
            try
            {
                r.baseValue = it.GetBaseValue();
            }
            catch
            {
                r.baseValue = 0;
            }
            try
            {
                r.desc = it.shortDescription;
                r.longDesc = it.longDescription;
                r.flavor = it.flavorText;
            }
            catch
            {
            }
            r.lower = (r.name + " " + id).ToLowerInvariant();

            it.Destroy();
            return r;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ItemCodex] Codex row '" + id + "' failed: " + e.Message);
            return null;
        }
    }

    // ---------------------------------------------------------------- 过滤
    private static bool Matches(Row r)
    {
        if (seenOnly && !CodexSeen.Has(r.id))
        {
            return false;
        }
        if (dirIndex > 0 && dirIndex <= dirIds.Count && dirIds[dirIndex] != "ALL" && r.dir != dirIds[dirIndex])
        {
            return false;
        }
        if (!string.IsNullOrEmpty(filterText))
        {
            string t = filterText.ToLowerInvariant();
            if (string.IsNullOrEmpty(t) || !r.lower.Contains(t))
            {
                return false;
            }
        }
        return true;
    }

    public static void ApplyFilter(string text)
    {
        if (text != null)
        {
            filterText = text;
        }
        if (window == null || listContent == null)
        {
            return;
        }
        int shown = 0;
        foreach (Row r in rows)
        {
            bool m = Matches(r);
            if (r.go != null)
            {
                r.go.SetActive(m);
            }
            if (m)
            {
                shown++;
            }
        }
        UpdateCount(shown);
        CustomUIBuilder.RebuildLayout(listContent);
    }

    private static void UpdateCount(int shown = -1)
    {
        if (countElement == null)
        {
            return;
        }
        if (shown < 0)
        {
            shown = 0;
            foreach (Row r in rows)
            {
                if (Matches(r))
                {
                    shown++;
                }
            }
        }
        try
        {
            countElement.SetText(ItemCodexEntry.L("codex_count", shown, rows.Count));
        }
        catch
        {
        }
    }

    // ---------------------------------------------------------------- 行/详情
    /// 复刻 GameItemElement.ResolveSpriteByName 的解析逻辑：
    /// 原版物品走 RenderHandler.LoadFromAtlas(atlasPath, spriteName)；MOD 物品走 LoadedMod.Sprites。
    private static Sprite ResolveItemSprite(string atlasPath, string spriteName, bool useModSprite)
    {
        try
        {
            if (string.IsNullOrEmpty(spriteName))
            {
                return null;
            }
            if (useModSprite)
            {
                LoadedMod mod = ModLoader.Mods.Find(m => m.Manifest.ID == atlasPath);
                if (mod != null && mod.Sprites.TryGetValue(spriteName, out Sprite s))
                {
                    return s;
                }
                return null;
            }
            if (string.IsNullOrEmpty(atlasPath))
            {
                return null;
            }
            return RenderHandler.LoadFromAtlas(atlasPath, spriteName);
        }
        catch
        {
            return null;
        }
    }

    private static void AddRowVisual(Row r)
    {
        try
        {
            CustomUIBuilder rb = new CustomUIBuilder(CustomUIManager.Instance, window, listContent);
            rb.AddButton(r.name + "   ·   " + r.baseValue, () => ShowDetail(r.id), null);
            r.go = (rb.Last != null && rb.Last.root != null) ? rb.Last.root : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ItemCodex] AddRowVisual failed: " + e.Message);
        }
    }

    private static void ShowDetail(string id)
    {
        if (window == null || detailHost == null)
        {
            return;
        }
        CustomUIBuilder.ClearContainer(detailHost);
        try
        {
            Func<GameItem> factory = DirectoryMaster.GetFactory<GameItem>(id);
            GameItem it = (factory != null) ? factory() : null;
            if (it != null)
            {
                CustomUIBuilder rb = new CustomUIBuilder(CustomUIManager.Instance, window, detailHost);
                rb.BeginRow();
                // 物品贴图必须像游戏本体一样按 图集路径+精灵名 解析
                // （CustomUIManager.GetSprite 查不到会返回 null，Image 预制体就会留着默认占位图）
                Sprite icon = ResolveItemSprite(it.spriteAtlasPath, it.spritePath, it.useSpriteFromMod);
                if (icon != null)
                {
                    rb.AddImage(icon, 56f, 56f, null);
                }
                rb.AddHeader(it.GetDisplayName());
                rb.End();
                rb.AddLabel(ItemCodexEntry.L("codex_detail_id", id), "codex_d_id");
                rb.AddLabel(ItemCodexEntry.L("codex_detail_value", it.GetBaseValue()), "codex_d_val");
                if (!string.IsNullOrEmpty(it.shortDescription))
                {
                    rb.AddLabel(it.shortDescription, "codex_d_sdesc");
                }
                if (!string.IsNullOrEmpty(it.longDescription))
                {
                    rb.AddLabel(it.longDescription, "codex_d_ldesc");
                }
                if (!string.IsNullOrEmpty(it.flavorText))
                {
                    rb.AddLabel(ItemCodexEntry.L("codex_flavor", it.flavorText), "codex_d_flavor");
                }
                it.Destroy();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ItemCodex] ShowDetail failed: " + e.Message);
        }
        CustomUIBuilder.RebuildLayout(detailHost);
    }
}
