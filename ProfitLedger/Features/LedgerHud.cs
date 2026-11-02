using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// 今日经营统计栏（利润小账本附属 HUD）
//   - 固定屏幕右上角最顶部（y=-48），顾客预览 HUD（customer.hud）在其下方，互不重叠
//   - 内容：今日 购X(红) · 售X(绿) · 净±X(按盈亏变色)
//   - 字体通过游戏官方 FontLoader.GetFontByRole 按当前语言分配
//   - 只受本 MOD 的"今日经营统计"开关控制，与顾客预览完全独立
// ============================================================================

public class LedgerHud : MonoBehaviour
{
    public static LedgerHud Instance { get; private set; }

    private GameObject hudPanel;
    private TextMeshProUGUI statsText;
    private TMP_FontAsset resolvedFont;
    private string lastLine = "";
    private bool lastShown;

    private static readonly Color PanelBg = new Color(0.05f, 0.06f, 0.09f, 0.78f);
    private const string NetGreen = "#7be07b";
    private const string NetRed = "#ff8080";

    public static LedgerHud Create()
    {
        GameObject go = new GameObject("ProfitLedger_Hud");
        UnityEngine.Object.DontDestroyOnLoad(go);
        return go.AddComponent<LedgerHud>();
    }

    private void Awake()
    {
        Instance = this;
        BuildHud();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void BuildHud()
    {
        try
        {
            GameObject canvasGo = new GameObject("ProfitLedger_Canvas", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            CustomUIManager.AddScreenScaler(canvasGo);

            hudPanel = new GameObject("stats_panel", typeof(RectTransform), typeof(Image));
            hudPanel.transform.SetParent(canvasGo.transform, false);
            Image bg = hudPanel.GetComponent<Image>();
            bg.color = PanelBg;
            bg.raycastTarget = false;

            RectTransform panelRt = hudPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(1f, 1f);
            panelRt.anchorMax = new Vector2(1f, 1f);
            panelRt.pivot = new Vector2(1f, 1f);
            panelRt.anchoredPosition = new Vector2(-16f, -48f); // 右上角最顶部
            panelRt.sizeDelta = new Vector2(380f, 0f);

            VerticalLayoutGroup vlg = hudPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 8, 8);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            ContentSizeFitter csf = hudPanel.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            statsText = CreateText();
            hudPanel.SetActive(false);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ProfitLedger] BuildHud failed: " + e.Message);
            hudPanel = null;
        }
    }

    private TextMeshProUGUI CreateText()
    {
        GameObject go = new GameObject("stats_text", typeof(RectTransform));
        go.transform.SetParent(hudPanel.transform, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }
        tmp.fontSize = 15;
        tmp.alignment = TextAlignmentOptions.MidlineRight;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 24f;
        le.flexibleWidth = 1f;
        return tmp;
    }

    private void TryResolveFont()
    {
        if (resolvedFont != null || statsText == null)
        {
            return;
        }
        try
        {
            if (FontLoader.Instance != null)
            {
                TMP_FontAsset f = FontLoader.Instance.GetFontByRole(FontLoader.FontRole.Noto_Sans, ModHelper.GetCurrentLocale());
                if (f != null)
                {
                    resolvedFont = f;
                    statsText.font = f;
                }
            }
        }
        catch (Exception)
        {
            // 本地化未初始化时静默重试
        }
    }

    private void Update()
    {
        ProfitLedgerEntry mod = ProfitLedgerEntry.Instance;
        if (mod == null || hudPanel == null || statsText == null)
        {
            return;
        }

        TryResolveFont();

        string line = "";
        bool shown = false;
        try
        {
            if (mod.dailyStatsHud != null && mod.dailyStatsHud.Value &&
                PlayerStore.HasInstance &&
                PlayerStore.Instance.storeState == PlayerStore.StoreState.OPEN)
            {
                long sold = DailyStats.SoldToday();
                long bought = DailyStats.BoughtToday();
                long net = sold - bought;
                string netHex = (net >= 0) ? NetGreen : NetRed;
                string netSign = (net >= 0) ? "+" : "";
                line = ProfitLedgerEntry.L("hud_today") + "  "
                    + "<color=" + NetRed + ">" + ProfitLedgerEntry.L("hud_bought", bought) + "</color>"
                    + " · "
                    + "<color=" + NetGreen + ">" + ProfitLedgerEntry.L("hud_sold", sold) + "</color>"
                    + " · "
                    + "<color=" + netHex + ">" + ProfitLedgerEntry.L("hud_net", netSign + net) + "</color>";
                shown = true;
            }
        }
        catch (Exception)
        {
            line = "";
            shown = false;
        }

        if (line == lastLine && shown == lastShown)
        {
            return;
        }
        lastLine = line;
        lastShown = shown;
        hudPanel.SetActive(shown);
        if (shown)
        {
            statsText.text = line;
        }
    }
}
