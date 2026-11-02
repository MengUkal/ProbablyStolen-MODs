using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GamepadPoc
{
    /// <summary>
    /// 通用 Transpiler：把方法体内所有 UnityEngine.Input 成员调用改写到 InputProxy。
    /// 以及按离线清单（PatchTargets.AssemblyCSharp）批量应用转写。
    /// </summary>
    public static class InputRewriter
    {
        // UnityEngine.Input 成员 → InputProxy 成员 的映射（签名一一对应）
        private static readonly Dictionary<string, string> ProxyMap = new Dictionary<string, string>
        {
            ["get_mousePosition"]    = "GetMousePosition",
            ["get_mouseScrollDelta"] = "GetMouseScrollDelta",
            ["GetMouseButton"]       = "GetMouseButton",
            ["GetMouseButtonDown"]   = "GetMouseButtonDown",
            ["GetMouseButtonUp"]     = "GetMouseButtonUp",
            ["GetKeyDown"]           = "GetKeyDown",
            ["GetKey"]               = "GetKey",
            ["GetAxis"]              = "GetAxis",
            ["GetAxisRaw"]           = "GetAxisRaw",
        };

        private static MethodInfo ResolveProxy(string name)
        {
            return AccessTools.Method(typeof(InputProxy), name)
                ?? throw new MissingMethodException("InputProxy", name);
        }

        /// <summary>Harmony transpiler 入口。先物化指令流，逐条检查 Input 调用并改写。</summary>
        public static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var ins in instructions.ToList())
            {
                if (ins.operand is MethodInfo m
                    && m.DeclaringType == typeof(Input)
                    && ProxyMap.TryGetValue(m.Name, out var proxyName))
                {
                    var proxy = ResolveProxy(proxyName);
                    bool sigOk = proxy.ReturnType == m.ReturnType
                        && proxy.GetParameters().Select(p => p.ParameterType)
                                .SequenceEqual(m.GetParameters().Select(p => p.ParameterType));
                    if (sigOk)
                    {
                        var ci = new CodeInstruction(OpCodes.Call, proxy);
                        ci.blocks.AddRange(ins.blocks);
                        ci.labels.AddRange(ins.labels);
                        yield return ci;
                        continue;
                    }
                }
                yield return ins;
            }
        }

        // ---------------- 批量应用 ----------------

        /// <summary>解析清单里的目标并全部打上转写补丁。返回 (已补丁, 未解析)。</summary>
        public static (int patched, List<string> unresolved) PatchAllTargets(Harmony harmony)
        {
            int patched = 0;
            var unresolved = new List<string>();
            var seen = new HashSet<MethodBase>();

            foreach (var spec in PatchTargets.AssemblyCSharp)
            {
                var parts = spec.Split(':');
                if (parts.Length != 3) { unresolved.Add(spec + " (bad spec)"); continue; }
                string typeName = parts[0], methodName = parts[1];
                if (!int.TryParse(parts[2], out int pcount)) { unresolved.Add(spec + " (bad count)"); continue; }

                var type = AccessTools.TypeByName(typeName);
                if (type == null) { unresolved.Add(spec + " (no type)"); continue; }

                MethodBase method = null;
                foreach (var cand in AccessTools.GetDeclaredMethods(type))
                {
                    if (cand.Name != methodName) continue;
                    if (cand.GetParameters().Length != pcount) continue;
                    if (cand.IsAbstract || cand.GetMethodBody() == null) continue;
                    method = cand;
                    break;
                }
                if (method == null) { unresolved.Add(spec + " (no method)"); continue; }
                if (!seen.Add(method)) continue;

                try
                {
                    harmony.Patch(method, transpiler: new HarmonyMethod(AccessTools.Method(typeof(InputRewriter), nameof(Rewrite))));
                    patched++;
                }
                catch (Exception e)
                {
                    unresolved.Add(spec + " (" + e.GetType().Name + ": " + e.Message + ")");
                }
            }
            return (patched, unresolved);
        }
    }

    /// <summary>uGUI 输入全部经由 BaseInput 虚方法 —— 直接 postfix 覆盖返回值（托管方法，可安全补丁）。</summary>
    [HarmonyPatch]
    internal static class BaseInputPatches
    {
        private static bool Gate => GamepadCursor.Active;

        [HarmonyPatch(typeof(BaseInput), "mousePosition", MethodType.Getter)]
        internal static class BI_MousePosition
        {
            internal static void Postfix(ref Vector2 __result)
            {
                if (Gate) __result = GamepadCursor.Instance.VirtualMousePosition;
            }
        }

        [HarmonyPatch(typeof(BaseInput), "mouseScrollDelta", MethodType.Getter)]
        internal static class BI_Scroll
        {
            internal static void Postfix(ref Vector2 __result)
            {
                if (Gate) __result = GamepadCursor.Instance.ScrollDelta;
            }
        }

        [HarmonyPatch(typeof(BaseInput), "GetMouseButtonDown", new[] { typeof(int) })]
        internal static class BI_Down
        {
            internal static void Postfix(int button, ref bool __result)
            {
                if (Gate) __result = GamepadCursor.Instance.GetVirtualMouseButtonDown(button);
            }
        }

        [HarmonyPatch(typeof(BaseInput), "GetMouseButtonUp", new[] { typeof(int) })]
        internal static class BI_Up
        {
            internal static void Postfix(int button, ref bool __result)
            {
                if (Gate) __result = GamepadCursor.Instance.GetVirtualMouseButtonUp(button);
            }
        }

        [HarmonyPatch(typeof(BaseInput), "GetMouseButton", new[] { typeof(int) })]
        internal static class BI_Hold
        {
            internal static void Postfix(int button, ref bool __result)
            {
                if (Gate) __result = GamepadCursor.Instance.GetVirtualMouseButton(button);
            }
        }
    }

    /// <summary>转写链路自检探针：打上同一个转写器后，其内部 Input 调用应被改写到代理。</summary>
    public static class RewriteProbe
    {
        public static Vector3 ReadMousePosition()
        {
            return Input.mousePosition;
        }
    }
}
