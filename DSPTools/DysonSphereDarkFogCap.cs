using HarmonyLib;

namespace DSPTools
{
    /// <summary>
    /// 黑雾截取戴森球发电量上限。
    ///
    /// 原版由 DysonSphere.energyDFHivesDebuffCoef 按太空巢穴的伽马接收器数量线性扣减发电量，
    /// 没有任何上限：每个伽马会让蜂群部分损失 0.03%、壳体部分损失 0.07%，
    /// 伽马足够多时扣减比例会超过 100%，甚至算出负数发电量。
    ///
    /// 这里直接 patch 属性 getter，让“发电量重算”和 UI 上显示的黑雾截取比例
    /// 使用同一个被限制过的系数，避免两边对不上。
    /// 由 LoadConfig.EnableDarkFogDysonDebuffCap 控制是否注册。
    /// </summary>
    [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.energyDFHivesDebuffCoef), MethodType.Getter)]
    internal static class DysonSphereDFDebuffCapPatch
    {
        internal const string HarmonyId = "Satroki.DSP.plugin.DSPTools.dfdyson";

        [HarmonyPostfix]
        private static void Postfix(ref double __result)
        {
            double capPercent = LoadConfig.DarkFogDysonDebuffCapPercent.Value;

            // 配置文件可能被手动改到范围外，这里再兜一次底
            if (capPercent < 0.0)
            {
                capPercent = 0.0;
            }
            else if (capPercent > 100.0)
            {
                capPercent = 100.0;
            }

            double minCoef = 1.0 - capPercent / 100.0;
            if (__result < minCoef)
            {
                __result = minCoef;
            }
        }
    }
}
