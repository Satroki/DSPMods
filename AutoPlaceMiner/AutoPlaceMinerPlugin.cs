using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace AutoPlaceMiner
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("DSPGAME.exe")]
    public sealed class AutoPlaceMinerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "Satroki.dsp.AutoPlaceMiner";
        public const string PluginName = "AutoPlaceMiner";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<KeyCode> Hotkey;
        internal static ConfigEntry<bool> SuppressCameraRoll;
        internal static ConfigEntry<bool> ShowResult;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Hotkey = Config.Bind(
                "General",
                "Hotkey",
                KeyCode.Q,
                "按下此键时，自动搜索大型采矿机覆盖矿脉最多的位置和角度并放置。");
            SuppressCameraRoll = Config.Bind(
                "General",
                "SuppressCameraRoll",
                true,
                "手持大型采矿机时，拦截 Q 的相机横滚功能，避免自动摆放时相机同时滚动。");
            ShowResult = Config.Bind(
                "General",
                "ShowResult",
                true,
                "在屏幕上显示自动摆放结果提示。");

            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll(typeof(CameraRollPatch));
            }
            catch (Exception exception)
            {
                Logger.LogWarning($"无法拦截相机横滚键，Q 可能同时滚动相机：{exception.Message}");
            }

            Logger.LogInfo($"{PluginName} v{PluginVersion} 已加载，快捷键：{Hotkey.Value}");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        private void Update()
        {
            if (Hotkey.Value == KeyCode.None || !Input.GetKeyDown(Hotkey.Value)) return;
            if (!Application.isFocused || VFInput.onGUI || VFInput.inputing) return;

            try
            {
                MinerPlacer.TryPlace();
            }
            catch (Exception exception)
            {
                Logger.LogError($"自动摆放大型采矿机失败：{exception}");
                Tip("自动摆放失败，请查看 BepInEx 日志");
            }
        }

        internal static void Tip(string message)
        {
            if (message != null && ShowResult != null && ShowResult.Value) UIRealtimeTip.Popup(message);
        }

        // 只用于判断是否要拦截 Q 的相机横滚。这里不要求已经打开建造模式，
        // 因为手持大型采矿机本身就意味着玩家正在准备建造。
        internal static bool IsAmmBuildMode()
        {
            var player = GameMain.mainPlayer;
            if (player == null || player.controller == null || player.inhandItemId <= 0) return false;
            var item = LDB.items.Select(player.inhandItemId);
            var desc = item?.prefabDesc;
            return desc != null && desc.veinMiner && desc.isVeinCollector;
        }
    }

    [HarmonyPatch(typeof(VFInput), "_rollLeft", MethodType.Getter)]
    internal static class CameraRollPatch
    {
        private static bool Prefix(ref VFInput.InputValue __result)
        {
            if (AutoPlaceMinerPlugin.SuppressCameraRoll == null ||
                !AutoPlaceMinerPlugin.SuppressCameraRoll.Value ||
                AutoPlaceMinerPlugin.Hotkey == null ||
                AutoPlaceMinerPlugin.Hotkey.Value != KeyCode.Q ||
                !AutoPlaceMinerPlugin.IsAmmBuildMode()) return true;

            __result = default;
            return false;
        }
    }
}
