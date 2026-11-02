using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace GamepadPoc
{
    /// <summary>XInput P/Invoke 轮询层。按 xinput1_4 → xinput1_3 → XInput9_1_0 顺序探测。</summary>
    public static class XInput
    {
        // ---- 按键掩码 ----
        public const ushort DPadUp = 0x0001, DPadDown = 0x0002, DPadLeft = 0x0004, DPadRight = 0x0008;
        public const ushort Start = 0x0010, Back = 0x0020;
        public const ushort LeftThumb = 0x0040, RightThumb = 0x0080;
        public const ushort LeftShoulder = 0x0100, RightShoulder = 0x0200;
        public const ushort A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000;
        public const short LeftDeadzone = 7849;   // XINPUT_GAMEPAD_LEFT_THUMB_DEADZONE
        public const short RightDeadzone = 8689;  // XINPUT_GAMEPAD_RIGHT_THUMB_DEADZONE

        public const int ErrorSuccess = 0;
        public const int ErrorDeviceNotConnected = 1167;

        [StructLayout(LayoutKind.Sequential)]
        public struct XInputGamepad
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XInputVibration
        {
            public ushort wLeftMotorSpeed;
            public ushort wRightMotorSpeed;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XInputState
        {
            public uint dwPacketNumber;
            public XInputGamepad Gamepad;
        }

        // ---- 手动 LoadLibrary 绑定（绕过 Mono 的 DllImport 解析器——它找不到 System32 的 xinput）----
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        public delegate int GetStateDelegate(int dwUserIndex, ref XInputState pState);
        private static GetStateDelegate _getState;
        public static string Backend { get; private set; } = "none";

        /// <summary>探测可用的 XInput 后端。返回 true 表示找到。</summary>
        public static bool Probe()
        {
            foreach (var name in new[] { "xinput1_4.dll", "xinput1_3.dll", "XInput9_1_0.dll" })
            {
                // 先按裸名加载（走 Windows 标准搜索路径含 System32），
                // 再试 SystemDirectory 绝对路径；不用 File.Exists 预检以保持零 files 能力
                IntPtr h = LoadLibrary(name);
                if (h == IntPtr.Zero)
                {
                    h = LoadLibrary(Path.Combine(Environment.SystemDirectory, name));
                }
                if (h == IntPtr.Zero) { Debug.Log($"[GamepadPoc] LoadLibrary({name}) failed"); continue; }
                IntPtr p = GetProcAddress(h, "XInputGetState");
                if (p == IntPtr.Zero) continue;
                _getState = (GetStateDelegate)Marshal.GetDelegateForFunctionPointer(p, typeof(GetStateDelegate));
                Backend = name;
                return true;
            }
            return false;
        }

        public static int GetState(int userIndex, ref XInputState state)
            => _getState != null ? _getState(userIndex, ref state) : ErrorDeviceNotConnected;

        /// <summary>归一化摇杆向量（含死区），x/y ∈ [-1,1]。</summary>
        public static UnityEngine.Vector2 Normalize(short rawX, short rawY, short deadzone)
        {
            float x = rawX, y = rawY;
            float mag = (float)Math.Sqrt(x * x + y * y);
            if (mag <= deadzone) return UnityEngine.Vector2.zero;
            float clamped = Math.Min(mag - deadzone, 32767f - deadzone);
            float scale = clamped / (32767f - deadzone) / mag;
            return new UnityEngine.Vector2(x * scale, y * scale);
        }
    }
}
