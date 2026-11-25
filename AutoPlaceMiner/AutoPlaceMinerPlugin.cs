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
        public const string PluginVersion = "1.3.1";

        internal static ManualLogSource Log;
        internal static ConfigEntry<KeyCode> Hotkey;
        internal static ConfigEntry<bool> SuppressCameraRoll;
        internal static ConfigEntry<bool> AutoFoundation;
        internal static ConfigEntry<bool> RestoreBuriedVeins;
        internal static ConfigEntry<bool> ShowResult;
        internal static ConfigEntry<bool> VerboseLog;

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
            AutoFoundation = Config.Bind(
                "General",
                "AutoFoundation",
                true,
                "水中矿脉自动铺地基。仅在没有更好的陆地位置时，为大型采矿机落点铺设地基，消耗地基物品和沙土；默认开启。");
            RestoreBuriedVeins = Config.Bind(
                "General",
                "RestoreBuriedVeins",
                true,
                "搜索前恢复当前矿脉群中被完全掩埋在地下的矿脉，恢复后再计算覆盖；被建筑/虚影占用的矿脉会跳过。默认开启。");
            ShowResult = Config.Bind(
                "General",
                "ShowResult",
                true,
                "在屏幕上显示自动摆放结果提示。");
            VerboseLog = Config.Bind(
                "General",
                "VerboseLog",
                false,
                "输出搜索、候选校验、地基等详细日志。Output detailed search, validation, and foundation logs.");

            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll(typeof(CameraRollPatch));
            }
            catch (Exception exception)
            {
                LogWarning($"无法拦截相机横滚键，Q 可能同时滚动相机：{exception.Message}");
            }

            LogInfo($"{PluginName} v{PluginVersion} 已加载，快捷键：{Hotkey.Value}，批量：Alt+{Hotkey.Value}");
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
                var batch = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                if (batch) MinerPlacer.TryPlaceOtherGroups();
                else MinerPlacer.TryPlace();
            }
            catch (Exception exception)
            {
                LogError($"自动摆放大型采矿机失败：{exception}");
                Tip(L10n.T("自动摆放失败，请查看 BepInEx 日志",
                    "Auto placement failed. Check the BepInEx log."));
            }
        }

        internal static void Tip(string message)
        {
            if (message != null && ShowResult != null && ShowResult.Value) UIRealtimeTip.Popup(message);
        }

        internal static bool Verbose => VerboseLog != null && VerboseLog.Value;

        internal static void LogInfo(string message)
        {
            Log?.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        internal static void LogWarning(string message)
        {
            Log?.LogWarning($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        internal static void LogError(string message)
        {
            Log?.LogError($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        internal static void VerboseInfo(string message)
        {
            if (Verbose) LogInfo(message);
        }

        internal static void VerboseWarning(string message)
        {
            if (Verbose) LogWarning(message);
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
