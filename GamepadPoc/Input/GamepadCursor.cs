using UnityEngine;

namespace GamepadPoc
{
    /// <summary>
    /// 手柄轮询 + 虚拟光标状态机（每帧 Update）。
    /// Active=true 时 InputPatches 用本状态覆盖游戏输入；false 时全部透传。
    /// </summary>
    public class GamepadCursor : MonoBehaviour
    {
        public static GamepadCursor Instance { get; private set; }

        /// <summary>虚拟化是否生效。</summary>
        public static bool Active => Instance != null
            && (Instance._connected || Instance._forceActive)
            && !Instance._suppressed;

        // ---- 供补丁层读取的虚拟状态 ----
        public Vector3 VirtualMousePosition => _cursorPos;
        public bool LeftDown => _leftHold;
        public bool LeftJustPressed => _leftPressed;
        public bool LeftJustReleased => _leftReleased;
        public bool RightDown => _rightHold;
        public bool RightJustPressed => _rightPressed;
        public bool RightJustReleased => _rightReleased;
        public Vector2 ScrollDelta => _scroll;

        public bool GetVirtualMouseButton(int button) => button switch
        {
            0 => _leftHold,
            1 => _rightHold,
            _ => false,
        };

        public bool GetVirtualMouseButtonDown(int button) => button switch
        {
            0 => _leftPressed,
            1 => _rightPressed,
            _ => false,
        };

        public bool GetVirtualMouseButtonUp(int button) => button switch
        {
            0 => _leftReleased,
            1 => _rightReleased,
            _ => false,
        };

        [Header("调参")]
        public float baseSpeed = 1100f;      // px/s @ 满偏移
        public float speedCurveExp = 1.7f;   // 偏移曲线指数（>1 = 慢启动）
        public float scrollSpeed = 900f;     // px/s 滚动速度（右摇杆）

        private bool _connected;
        private bool _everConnected;
        private bool _suppressed;            // 无手柄时的自检窗口外强制关闭
        private bool _forceActive;           // 自检窗口强制虚拟化（无硬件验证链路）

        private Vector3 _cursorPos;
        private bool _leftHold, _leftPressed, _leftReleased;
        private bool _rightHold, _rightPressed, _rightReleased;
        private Vector2 _scroll;

        private XInput.XInputState _state;
        private ushort _prevButtons;
        private GameObject _overlay;
        private RectTransform _overlayRect;
        private Canvas _canvas;

        public static void Create()
        {
            if (Instance != null) return;
            var go = new GameObject("GamepadPoc_Cursor");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GamepadCursor>();
        }

        private void Update()
        {
            _leftPressed = _leftReleased = _rightPressed = _rightReleased = false;
            _scroll = Vector2.zero;

            int rc = XInput.GetState(0, ref _state);
            bool nowConnected = rc == XInput.ErrorSuccess;
            if (nowConnected != _connected)
            {
                _connected = nowConnected;
                Debug.Log($"[GamepadPoc] controller {(nowConnected ? "connected" : "disconnected")} (rc={rc})");
                if (nowConnected) { _everConnected = true; EnsureCursorWithinScreen(); }
                SetOsCursorVisible(!_connected);
            }
            if (!_connected)
            {
                // 自检窗口（无硬件时验证补丁链路）
                SelfTest.Tick();
                return;
            }

            var g = _state.Gamepad;
            Vector2 stick = XInput.Normalize(g.sThumbLX, g.sThumbLY, XInput.LeftDeadzone);
            Vector2 rStick = XInput.Normalize(g.sThumbRX, g.sThumbRY, XInput.RightDeadzone);

            // 摇杆 → 光标速度（帧率无关）
            float def = stick.magnitude;
            if (def > 0f)
            {
                float speed = baseSpeed * Mathf.Pow(def, speedCurveExp);
                Vector2 v = stick.normalized * speed * Time.unscaledDeltaTime;
                _cursorPos.x = Mathf.Clamp(_cursorPos.x + v.x, 0f, Screen.width - 1f);
                _cursorPos.y = Mathf.Clamp(_cursorPos.y + v.y, 0f, Screen.height - 1f);
            }

            // 右摇杆 Y → 滚轮
            if (Mathf.Abs(rStick.y) > 0.01f)
            {
                _scroll = new Vector2(0f, rStick.y * scrollSpeed * Time.unscaledDeltaTime);
            }

            // 按键沿
            bool a = (g.wButtons & XInput.A) != 0;
            bool b = (g.wButtons & XInput.B) != 0;
            _leftPressed = a && !_leftHold;
            _leftReleased = !a && _leftHold;
            _leftHold = a;
            _rightPressed = b && !_rightHold;
            _rightReleased = !b && _rightHold;
            _rightHold = b;
            _prevButtons = g.wButtons;

            if (_overlayRect != null) _overlayRect.position = _cursorPos;
        }

        private void EnsureCursorWithinScreen()
        {
            _cursorPos.x = Mathf.Clamp(_cursorPos.x, 0f, Screen.width - 1f);
            _cursorPos.y = Mathf.Clamp(_cursorPos.y, 0f, Screen.height - 1f);
        }

        public void SetSuppress(bool on)
        {
            _suppressed = on;
            if (_overlay != null) _overlay.SetActive(Active);
        }

        /// <summary>自检窗口内强制虚拟化（无硬件验证转写链路）。</summary>
        public void SetForceActive(bool on)
        {
            _forceActive = on;
            if (_overlay != null) _overlay.SetActive(Active);
            if (_overlay != null && Active) _overlayRect.position = _cursorPos;
        }

        // ---------------- 覆盖光标 ----------------
        public void EnsureOverlay()
        {
            if (_overlay != null) return;
            _canvas = new GameObject("GamepadPoc_OverlayCanvas").AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 30000;
            DontDestroyOnLoad(_canvas.gameObject);

            _overlay = new GameObject("GamepadPoc_CursorSprite");
            _overlay.transform.SetParent(_canvas.transform, false);
            var img = _overlay.AddComponent<UnityEngine.UI.Image>();
            img.sprite = MakeCircleSprite(28, 3);
            img.raycastTarget = false;   // 绝不拦截 UI 射线
            _overlayRect = img.rectTransform;
            _overlay.SetActive(Active);
        }

        private static Sprite MakeCircleSprite(int size, int border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                    Color col = Color.clear;
                    if (d <= c - border) col = new Color(1f, 1f, 1f, 0.95f);       // 白色内圈
                    else if (d <= c) col = new Color(0f, 0f, 0f, 0.95f);           // 黑色描边
                    tex.SetPixel(x, y, col);
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void SetOsCursorVisible(bool visible)
        {
            try { Cursor.visible = visible; } catch { /* 某些模式会抛出 */ }
        }

        private void OnApplicationQuit()
        {
            SetOsCursorVisible(true);
        }

        private void OnDestroy()
        {
            SetOsCursorVisible(true);
        }
    }

    /// <summary>无手柄硬件时的自检：开一个 6 秒窗口，强制虚拟化并画圆轨迹，验证补丁链路。</summary>
    public static class SelfTest
    {
        private enum Phase { Idle, Running, Done, Failed }
        private static Phase _phase = Phase.Idle;
        private static float _t;
        private static Vector3 _center;
        private static int _samples;
        private static int _okSamples;
        private const float Radius = 120f;
        private const float Duration = 6f;

        public static void Tick()
        {
            if (GamepadCursor.Instance == null || GamepadCursor.Instance.enabled == false) return;
            var inst = GamepadCursor.Instance;
            switch (_phase)
            {
                case Phase.Idle:
                    // 等 2 秒让所有系统就绪再开始
                    _t += Time.unscaledDeltaTime;
                    if (_t >= 2f)
                    {
                        _phase = Phase.Running;
                        _t = 0f;
                        _center = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
                        inst.SetForceActive(true);
                        inst.EnsureOverlay();
                        Cursor.visible = false;
                        Debug.Log($"[GamepadPoc][SelfTest] start (no controller) — forcing virtual input for {Duration}s");
                    }
                    break;

                case Phase.Running:
                    _t += Time.unscaledDeltaTime;
                    float ang = _t * 2f;   // 2 rad/s
                    Vector3 expected = _center + new Vector3(Mathf.Cos(ang) * Radius, Mathf.Sin(ang) * Radius, 0f);
                    // 直接写状态机的光标位置（模拟摇杆驱动结果）
                    var f = typeof(GamepadCursor).GetField("_cursorPos",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    f.SetValue(inst, expected);

                    // 读回 —— 经转写探针：若转写链路生效，ReadMousePosition 内部的
                    // Input.mousePosition 已被改写到 InputProxy，应返回虚拟位置
                    Vector3 readBack = RewriteProbe.ReadMousePosition();
                    bool ok = (readBack - expected).sqrMagnitude < 4f;
                    _samples++;
                    if (ok) _okSamples++;

                    if (_t >= Duration)
                    {
                        bool pass = _okSamples >= _samples * 0.9f;
                        Debug.Log($"[GamepadPoc][SelfTest] {(pass ? "PASS" : "FAIL")} — {_okSamples}/{_samples} samples match (Input call rewritten to proxy)");
                        _phase = pass ? Phase.Done : Phase.Failed;
                        inst.SetForceActive(false);
                        Cursor.visible = true;
                    }
                    break;
            }
        }
    }
}
