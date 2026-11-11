using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace DSPTools
{
    /// <summary>
    /// 把配置里的自定义分辨率注入游戏的分辨率列表。
    ///
    /// 游戏的下拉框内容来自 GameResolutions.InitResolutions() 读取的 Screen.resolutions，
    /// 而且 GameOption.Apply() 会拿当前设置去这份列表里逐项校验（宽、高、刷新率），
    /// 匹配不上就 LogWarning 并回退到列表里最大的一档。
    /// 所以必须在 Apply() 之前把条目塞进列表，否则 options.xml 里的自定义值会被静默改掉。
    ///
    /// 游戏里的三份列表分别是：
    ///   rawResolutions      —— 独占全屏，按刷新率拆分，标签显示 Hz
    ///   mergedResolutions   —— 无边框全屏，按分辨率去重，标签不显示 Hz
    ///   windowedResolutions —— 窗口模式，第一条是“刚好放得下”的最大窗口尺寸
    /// 三份都要加，否则切换显示模式后列表里就看不到了。
    /// </summary>
    internal static class CustomResolutionInjector
    {
        private static readonly FieldInfo RawField = AccessTools.Field(typeof(GameResolutions), "rawResolutions");
        private static readonly FieldInfo MergedField = AccessTools.Field(typeof(GameResolutions), "mergedResolutions");
        private static readonly FieldInfo WindowedField = AccessTools.Field(typeof(GameResolutions), "windowedResolutions");

        // 宽x高[@刷新率]，刷新率后面允许跟 Hz
        private static readonly Regex SpecPattern = new Regex(
            @"^\s*(\d+)\s*[xX×*]\s*(\d+)\s*(?:@\s*([0-9]+(?:\.[0-9]+)?)\s*[hH]?[zZ]?)?\s*$",
            RegexOptions.Compiled);

        private static readonly char[] Separators = { ',', ';', '\n', '\r', '|' };

        private const int MaxDimension = 16384;

        /// <summary>幂等：可以重复调用，已经在列表里的分辨率不会重复添加。</summary>
        internal static void Inject()
        {
            if (!LoadConfig.EnableCustomResolution.Value)
            {
                return;
            }

            string configured = LoadConfig.CustomResolutions.Value;
            if (string.IsNullOrWhiteSpace(configured))
            {
                return;
            }

            var raw = RawField?.GetValue(null) as List<GameResolutions.Resolution>;
            var merged = MergedField?.GetValue(null) as List<GameResolutions.Resolution>;
            var windowed = WindowedField?.GetValue(null) as List<GameResolutions.Resolution>;
            if (raw == null || merged == null || windowed == null)
            {
                // 游戏版本变了、字段改名了，安静地什么都不做，别把游戏搞崩
                return;
            }

            try
            {
                double fallbackRefreshRate = GetDisplayRefreshRate();
                bool changed = false;
                List<string> added = new List<string>();

                foreach (string item in configured.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!TryParse(item, fallbackRefreshRate, out int width, out int height, out double hz))
                    {
                        continue;
                    }

                    RefreshRate rawRate = GameResolutions.DoubleToRefreshRate(hz);
                    // 无边框/窗口模式的标签里不带 Hz，和游戏自己构造 merged 列表的做法保持一致
                    RefreshRate hiddenRate = rawRate;
                    hiddenRate.numerator = 0;

                    bool addedRaw = AddEntry(raw, width, height, rawRate, rawRate);
                    bool addedMerged = AddEntry(merged, width, height, rawRate, hiddenRate);
                    bool addedWindowed = AddEntry(windowed, width, height, rawRate, hiddenRate);
                    if (addedRaw || addedMerged || addedWindowed)
                    {
                        changed = true;
                        added.Add($"{width}x{height}@{hz.ToString("0.###", CultureInfo.InvariantCulture)}");
                    }
                }

                if (!changed)
                {
                    return;
                }

                // 游戏按 高度 > 宽度 > 刷新率 降序排列；追加的条目必须重排，否则会全部堆在下拉框末尾
                raw.Sort(CompareDescending);
                merged.Sort(CompareDescending);
                windowed.Sort(CompareDescending);

                Debug.Log("[DSPTools] 已注入自定义分辨率: " + string.Join(", ", added));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DSPTools] 注入自定义分辨率失败: " + ex);
            }
        }

        private static bool AddEntry(List<GameResolutions.Resolution> list, int width, int height, RefreshRate rawRate, RefreshRate formattedRate)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                GameResolutions.Resolution existing = list[i];
                // RefreshRate.Equals 是交叉相乘比较，165/1 与 165000/1000 视为相同
                if (existing.width == width && existing.height == height && existing.rawRefreshRate.Equals(rawRate))
                {
                    return false;
                }
            }

            list.Add(new GameResolutions.Resolution
            {
                width = width,
                height = height,
                rawRefreshRate = rawRate,
                formattedRefreshRate = formattedRate
            });
            return true;
        }

        private static int CompareDescending(GameResolutions.Resolution lhs, GameResolutions.Resolution rhs)
        {
            if (lhs.height != rhs.height)
            {
                return rhs.height.CompareTo(lhs.height);
            }

            if (lhs.width != rhs.width)
            {
                return rhs.width.CompareTo(lhs.width);
            }

            return rhs.rawRefreshRate.value.CompareTo(lhs.rawRefreshRate.value);
        }

        private static bool TryParse(string text, double fallbackRefreshRate, out int width, out int height, out double hz)
        {
            width = 0;
            height = 0;
            hz = fallbackRefreshRate;

            Match match = SpecPattern.Match(text);
            if (!match.Success)
            {
                return false;
            }

            if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out width) ||
                !int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out height))
            {
                return false;
            }

            if (match.Groups[3].Success &&
                double.TryParse(match.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            {
                hz = parsed;
            }

            return width > 0 && height > 0 && width <= MaxDimension && height <= MaxDimension &&
                   hz > 0 && hz <= 1000;
        }

        private static double GetDisplayRefreshRate()
        {
            try
            {
                double hz = Screen.currentResolution.refreshRateRatio.value;
                return hz > 0 && !double.IsNaN(hz) ? hz : 60.0;
            }
            catch
            {
                return 60.0;
            }
        }
    }

    /// <summary>InitResolutions() 只在 GameOption.LoadGlobal() 里调用一次，紧跟其后补上自定义条目。</summary>
    [HarmonyPatch(typeof(GameResolutions), nameof(GameResolutions.InitResolutions))]
    internal static class GameResolutionsInitPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            CustomResolutionInjector.Inject();
        }
    }

    /// <summary>兜底：万一插件加载得比较晚、错过了 InitResolutions，也要在 Apply() 校验之前把条目补进去。</summary>
    [HarmonyPatch(typeof(GameOption), nameof(GameOption.Apply))]
    internal static class GameOptionApplyPatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            CustomResolutionInjector.Inject();
        }
    }
}
