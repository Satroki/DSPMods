using ABN;
using HarmonyLib;

namespace DSPTools
{
    /// <summary>
    /// 装 mod 也允许解锁成就。
    ///
    /// 游戏原版通过“异常检测”（AbnormalityLogic / GameAbnormalityData_0925）把改过的存档标记为异常，
    /// AchievementLogic.active 因此返回 false，成就就不再触发；读档时还会把历史异常数据一起载入。
    /// 这里屏蔽异常检测与异常记录，并在读档后清空异常数据，同时强制 active 为 true。
    ///
    /// 功能思路来自 Acceyuriko 的 EnableAchievements（DSPYurikoPlugin），由 DSPTools 内置一份，
    /// 安装本模组后就不再需要单独安装那个 mod。
    /// </summary>
    internal static class AchievementFeature
    {
        internal const string HarmonyId = "Satroki.DSP.plugin.DSPTools.achievement";
    }

    /// <summary>AchievementLogic.active 的 getter：直接返回 true，跳过异常/沙盒/正式存档等判断。</summary>
    [HarmonyPatch(typeof(AchievementLogic), "active", MethodType.Getter)]
    internal static class AchievementActivePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }

    /// <summary>停掉每帧的异常检测，避免 mod 改数据时被记录下来。</summary>
    [HarmonyPatch(typeof(AbnormalityLogic), "GameTick")]
    internal static class AbnormalityGameTickPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return false;
        }
    }

    /// <summary>直接拦掉异常上报，存档里不会新增异常记录。</summary>
    [HarmonyPatch(typeof(GameAbnormalityData_0925), "TriggerAbnormality")]
    internal static class AbnormalityTriggerPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return false;
        }
    }

    /// <summary>读档后清空历史异常数据，老存档也能正常解锁成就。</summary>
    [HarmonyPatch(typeof(GameAbnormalityData_0925), "Import")]
    internal static class AbnormalityImportPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameAbnormalityData_0925 __instance)
        {
            if (__instance.runtimeDatas != null)
            {
                System.Array.Clear(__instance.runtimeDatas, 0, __instance.runtimeDatas.Length);
            }
        }
    }
}
