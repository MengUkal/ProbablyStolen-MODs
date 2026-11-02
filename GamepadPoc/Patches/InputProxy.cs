using UnityEngine;

namespace GamepadPoc
{
    /// <summary>
    /// UnityEngine.Input 的签名兼容代理。
    /// 转写器把游戏方法里的 Input.X 调用改写到本类的同名方法。
    /// Active=false 时 100% 透传真实输入，鼠标玩家零影响。
    /// 方法签名必须与 Input 完全一致（参数类型 + 返回类型）。
    /// </summary>
    public static class InputProxy
    {
        public static Vector3 GetMousePosition()
        {
            if (GamepadCursor.Active) return GamepadCursor.Instance.VirtualMousePosition;
            return Input.mousePosition;
        }

        public static Vector2 GetMouseScrollDelta()
        {
            if (GamepadCursor.Active) return GamepadCursor.Instance.ScrollDelta;
            return Input.mouseScrollDelta;
        }

        public static bool GetMouseButton(int button)
        {
            if (GamepadCursor.Active) return GamepadCursor.Instance.GetVirtualMouseButton(button);
            return Input.GetMouseButton(button);
        }

        public static bool GetMouseButtonDown(int button)
        {
            if (GamepadCursor.Active) return GamepadCursor.Instance.GetVirtualMouseButtonDown(button);
            return Input.GetMouseButtonDown(button);
        }

        public static bool GetMouseButtonUp(int button)
        {
            if (GamepadCursor.Active) return GamepadCursor.Instance.GetVirtualMouseButtonUp(button);
            return Input.GetMouseButtonUp(button);
        }

        public static bool GetKeyDown(KeyCode key)
        {
            // POC 阶段按键不做虚拟化，仅透传
            return Input.GetKeyDown(key);
        }

        public static bool GetKey(KeyCode key)
        {
            return Input.GetKey(key);
        }

        public static float GetAxis(string axisName)
        {
            return Input.GetAxis(axisName);
        }

        public static float GetAxisRaw(string axisName)
        {
            return Input.GetAxisRaw(axisName);
        }
    }
}
