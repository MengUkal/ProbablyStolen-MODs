using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace GamepadPoc
{
    /// <summary>POC 入口。ModLoader 先自动 PatchAll（装 BaseInput postfix），再调 Init/OnEnable。</summary>
    public class PocEntry : IMod
    {
        public const string ModId = "gamepad.poc";
        public static PocEntry Instance { get; private set; }
        private ModManifest _manifest;
        private Harmony _harmony;

        public void Init(ModManifest manifest)
        {
            Instance = this;
            _manifest = manifest;
            Debug.Log("[GamepadPoc] Init");

            bool probed = XInput.Probe();
            Debug.Log($"[GamepadPoc] XInput backend: {(probed ? XInput.Backend : "NOT FOUND")}（无手柄时自检窗口仍会验证转写链路）");
        }

        public void OnEnable()
        {
            GamepadCursor.Create();

            _harmony = new Harmony("gamepad.poc.proxy");

            // 1) 给自检探针打上同一个转写器（验证整条链路）
            _harmony.Patch(
                AccessTools.Method(typeof(RewriteProbe), "ReadMousePosition"),
                transpiler: new HarmonyMethod(AccessTools.Method(typeof(InputRewriter), nameof(InputRewriter.Rewrite))));

            // 2) 按离线清单批量转写游戏方法
            var (patched, unresolved) = InputRewriter.PatchAllTargets(_harmony);
            Debug.Log($"[GamepadPoc] transpiler: {patched}/{PatchTargets.AssemblyCSharp.Length} game methods rewritten");
            if (unresolved.Count > 0)
            {
                Debug.LogWarning("[GamepadPoc] unresolved targets (" + unresolved.Count + "): "
                    + string.Join("; ", unresolved.Take(12)) + (unresolved.Count > 12 ? " ..." : ""));
            }

            Debug.Log("[GamepadPoc] OnEnable: done");
        }

        public void OnDisable()
        {
            Debug.Log("[GamepadPoc] OnDisable");
            _harmony?.UnpatchAll("gamepad.poc.proxy");
            _harmony = null;
            Instance = null;
        }
    }
}
