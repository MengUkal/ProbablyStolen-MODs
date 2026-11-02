using System;
using UnityEngine;

// ============================================================================
// 宿主：常驻 MonoBehaviour（物品图鉴）
//   - 图鉴快捷键轮询
//   - 图鉴搜索输入的防抖回调
// ============================================================================

public class CodexHost : MonoBehaviour
{
    public static CodexHost Instance { get; private set; }

    private float filterDebounce = -1f;
    private string pendingFilter;

    public static CodexHost Create()
    {
        GameObject go = new GameObject("ItemCodex_Host");
        UnityEngine.Object.DontDestroyOnLoad(go);
        return go.AddComponent<CodexHost>();
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetFilterDebounce(string text, bool immediate)
    {
        pendingFilter = text;
        filterDebounce = immediate ? 0f : 0.35f;
    }

    private void Update()
    {
        ItemCodexEntry mod = ItemCodexEntry.Instance;
        if (mod == null)
        {
            return;
        }

        // ---- 图鉴快捷键 ----
        if (mod.codexKey != KeyCode.None && Input.GetKeyDown(mod.codexKey))
        {
            ItemCodex.Toggle();
        }

        // ---- 图鉴搜索防抖 ----
        if (filterDebounce >= 0f)
        {
            filterDebounce -= Time.unscaledDeltaTime;
            if (filterDebounce < 0f)
            {
                ItemCodex.ApplyFilter(pendingFilter);
                pendingFilter = null;
            }
        }
    }
}
