using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// 顾客面板宿主：常驻 MonoBehaviour（customer.hud 专属）
//   - 面板固定右上角第二层（y=-112），其上方是利润小账本的"今日经营统计"栏
//   - 优先显示 下一位顾客（头像+名字+排队人数）；队列空但正在接待时显示"正在接待：X"
//   - 布局为 面板容器(Vertical) + 内部行(Horizontal) 两层，切勿合并到同一节点
//   - 字体通过游戏官方 FontLoader.GetFontByRole 按当前语言分配
//   - 跳过顾客快捷键轮询
// ============================================================================

public class HudHost : MonoBehaviour
{
    public static HudHost Instance { get; private set; }

    private GameObject hudPanel;
    private Image avatarImage;
    private LayoutElement avatarLe;
    private TextMeshProUGUI customerText;
    private TMP_FontAsset resolvedFont;
    private Sprite lastAvatar;
    private string lastHud = "";

    private static readonly Color PanelBg = new Color(0.05f, 0.06f, 0.09f, 0.78f);
    private const float AvatarHeight = 88f;   // 头像高度（像素）
    private const float AvatarMinWidth = 40f;
    private const float AvatarMaxWidth = 150f;

    public static HudHost Create()
    {
        GameObject go = new GameObject("CustomerHud_Host");
        UnityEngine.Object.DontDestroyOnLoad(go);
        return go.AddComponent<HudHost>();
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
            GameObject canvasGo = new GameObject("CustomerHud_Canvas", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            CustomUIManager.AddScreenScaler(canvasGo); // 复用游戏的缩放适配

            // ---- 面板容器（只有 VerticalLayoutGroup，避免双布局组冲突）----
            hudPanel = new GameObject("hud_panel", typeof(RectTransform), typeof(Image));
            hudPanel.transform.SetParent(canvasGo.transform, false);
            Image bg = hudPanel.GetComponent<Image>();
            bg.color = PanelBg;
            bg.raycastTarget = false;

            RectTransform panelRt = hudPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(1f, 1f);
            panelRt.anchorMax = new Vector2(1f, 1f);
            panelRt.pivot = new Vector2(1f, 1f);
            panelRt.anchoredPosition = new Vector2(-16f, -112f); // 统计栏（y=-48）下方，互不重叠
            panelRt.sizeDelta = new Vector2(380f, 0f);

            VerticalLayoutGroup vlg = hudPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 10, 10);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            ContentSizeFitter csf = hudPanel.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // ---- 内部行（Horizontal 布局挂在子节点上）----
            GameObject row = new GameObject("row", typeof(RectTransform));
            row.transform.SetParent(hudPanel.transform, false);
            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            LayoutElement rowLe = row.AddComponent<LayoutElement>();
            rowLe.preferredHeight = AvatarHeight;

            GameObject avatarGo = new GameObject("avatar", typeof(RectTransform), typeof(Image));
            avatarGo.transform.SetParent(row.transform, false);
            avatarImage = avatarGo.GetComponent<Image>();
            avatarImage.raycastTarget = false;
            avatarImage.preserveAspect = true; // 防止立绘被拉伸变形
            avatarLe = avatarGo.AddComponent<LayoutElement>();
            avatarLe.preferredWidth = AvatarMinWidth;
            avatarLe.preferredHeight = AvatarHeight;

            customerText = CreateText(row.transform, "customer_text");
            customerText.color = new Color(0.95f, 0.95f, 0.95f);

            hudPanel.SetActive(false);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CustomerHud] BuildHud failed: " + e.Message);
            hudPanel = null;
        }
    }

    private TextMeshProUGUI CreateText(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }
        tmp.fontSize = 17;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.preferredHeight = AvatarHeight;
        return tmp;
    }

    private void TryResolveFont()
    {
        if (resolvedFont != null || customerText == null)
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
                    customerText.font = f;
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
        CustomerHudEntry mod = CustomerHudEntry.Instance;
        if (mod == null)
        {
            return;
        }

        if (mod.skipKey != KeyCode.None && Input.GetKeyDown(mod.skipKey))
        {
            TrySkipClient();
        }

        TryResolveFont();
        UpdateHud(mod);
    }

    private void TrySkipClient()
    {
        if (!PlayerStore.HasInstance)
        {
            return;
        }
        PlayerStore ps = PlayerStore.Instance;
        if (ps == null || ps.storeState != PlayerStore.StoreState.OPEN || ps.currentClientInstance == null)
        {
            return;
        }
        try
        {
            ps.TryPlayerDismissCurrentClient(true);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CustomerHud] skip client failed: " + e.Message);
        }
    }

    private void UpdateHud(CustomerHudEntry mod)
    {
        if (hudPanel == null || customerText == null)
        {
            return;
        }

        string line = "";
        Sprite avatar = null;

        try
        {
            if (mod.customerHud != null && mod.customerHud.Value &&
                PlayerStore.HasInstance &&
                PlayerStore.Instance.storeState == PlayerStore.StoreState.OPEN)
            {
                PlayerStore ps = PlayerStore.Instance;
                StoreClientManager mgr = ps.storeClientManager;
                bool hasQueue = mgr != null && mgr.clientStack != null && mgr.clientStack.Count > 0;

                if (hasQueue)
                {
                    // 队列里还有人：显示下一位
                    StoreClient next = mgr.clientStack[0];
                    string name = (next != null) ? next.displayName : "?";
                    line = CustomerHudEntry.L("hud_next", name) + "  ·  " + CustomerHudEntry.L("hud_queue", mgr.clientStack.Count);
                    if (next != null)
                    {
                        avatar = TryGetClientSprite(next);
                    }
                }
                else if (ps.currentClientInstance != null)
                {
                    // 队列空但正在接待：显示当前顾客，避免面板"凭空消失"
                    StoreClient current = ps.currentClientInstance.GetClientBlueprint();
                    if (current != null)
                    {
                        line = CustomerHudEntry.L("hud_serving", current.displayName);
                        avatar = TryGetClientSprite(current);
                    }
                }
            }
        }
        catch (Exception)
        {
            line = "";
            avatar = null;
        }

        string stateKey = line + "|" + (avatar != null);
        if (stateKey == lastHud)
        {
            return;
        }
        lastHud = stateKey;

        bool show = line.Length > 0;
        hudPanel.SetActive(show);
        if (!show)
        {
            return;
        }

        customerText.text = line;
        bool hasAvatar = avatar != null;
        avatarImage.gameObject.SetActive(hasAvatar);
        if (hasAvatar && avatar != lastAvatar)
        {
            avatarImage.sprite = avatar;
            lastAvatar = avatar;
            // 按精灵真实宽高比分配框宽，高度固定（preserveAspect 兜底防拉伸）
            float w = AvatarHeight;
            if (avatar.rect.height > 1f)
            {
                w = Mathf.Clamp(AvatarHeight * (avatar.rect.width / avatar.rect.height), AvatarMinWidth, AvatarMaxWidth);
            }
            avatarLe.preferredWidth = w;
            avatarLe.preferredHeight = AvatarHeight;
        }
    }

    private static Sprite TryGetClientSprite(StoreClient client)
    {
        try
        {
            return SpriteDict.Instance.ShowClientSprite(client.GetSprite());
        }
        catch
        {
            return null;
        }
    }
}
