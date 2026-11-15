using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace AutoConnectPower
{
    /// <summary>连接塔的建造策略。</summary>
    public enum TowerMode
    {
        /// <summary>只使用电力感应塔。</summary>
        TeslaOnly,

        /// <summary>长距离连接时交替使用无线输电塔和电力感应塔。</summary>
        Alternate,
    }

    /// <summary>连接塔的落成方式。</summary>
    public enum PlacementMode
    {
        /// <summary>直接从背包消耗材料并建成。</summary>
        Direct,

        /// <summary>只生成未建造虚影，材料/建造交给游戏原本的建造无人机流程。</summary>
        Ghost,
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("DSPGAME.exe")]
    public sealed class AutoConnectPowerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "Satroki.dsp.AutoConnectPower";
        public const string PluginName = "AutoConnectPower";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<TowerMode> TowerModeConfig;
        internal static ConfigEntry<PlacementMode> PlacementModeConfig;
        internal static ConfigEntry<bool> AutoFoundation;
        internal static ConfigEntry<bool> ShowResult;
        internal static ConfigEntry<KeyboardShortcut> Hotkey;

        private void Awake()
        {
            Log = Logger;

            TowerModeConfig = Config.Bind(
                "General",
                "TowerMode",
                TowerMode.Alternate,
                "连接塔类型。TeslaOnly = 只使用电力感应塔；Alternate = 长距离连接时交替使用无线输电塔与电力感应塔。");
            PlacementModeConfig = Config.Bind(
                "General",
                "PlacementMode",
                PlacementMode.Ghost,
                "落成方式。Ghost = 只生成未建造虚影（默认），由建造无人机或玩家补材料建造；Direct = 直接从背包消耗材料并建成。");
            AutoFoundation = Config.Bind(
                "General",
                "AutoFoundation",
                true,
                "连接塔落点在水面/熔岩等液体表面时自动铺地基（3×3 格，消耗地基物品和沙土；已是地基的格不重复消耗）。默认开启。地面上的塔不铺地基；液体表面塔地基材料/沙土不足时会被跳过。");
            ShowResult = Config.Bind(
                "General",
                "ShowResult",
                true,
                "在屏幕右下角显示执行结果提示。");

            Hotkey = Config.Bind(
                "General",
                "Hotkey",
                new KeyboardShortcut(KeyCode.C, KeyCode.LeftAlt),
                "自动连接电网快捷键（默认 Alt+C，留空/None 关闭）。格式示例：C + LeftAlt；也可以只写一个键，如 F8。");

            Logger.LogInfo($"{PluginName} v{PluginVersion} 已加载，快捷键 {Hotkey.Value}。");
        }

        private void Update()
        {
            if (Hotkey == null || !IsHotkeyDown(Hotkey.Value)) return;
            if (!Application.isFocused || VFInput.onGUI || VFInput.inputing) return;
            if (GameMain.data == null || GameMain.localPlanet == null || GameMain.mainPlayer == null) return;

            try
            {
                PowerGridConnector.Run();
            }
            catch (Exception exception)
            {
                Logger.LogError("自动连接电网失败：" + exception);
                Tip("自动连接电网失败，请查看 BepInEx 日志");
            }
        }

        // 只要求快捷键里的主键和修饰键按下，不阻止玩家同时按着移动键或其它游戏按键。
        private static bool IsHotkeyDown(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None) return false;
            if (!Input.GetKeyDown(shortcut.MainKey)) return false;

            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (!Input.GetKey(modifier)) return false;
            }

            return true;
        }

        internal static void Tip(string message)
        {
            if (message == null) return;
            if (ShowResult != null && !ShowResult.Value) return;
            if (GameMain.data == null || GameMain.mainPlayer == null) return;
            UIRealtimeTip.Popup(message);
        }
    }
}
