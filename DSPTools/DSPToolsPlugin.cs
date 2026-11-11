using BepInEx;
using HarmonyLib;
using System;

namespace DSPTools
{
    [BepInPlugin("Satroki.DSP.plugin.DSPTools", "DSPTools", "1.0.0")]
    [BepInProcess("DSPGAME.exe")]
    public class DSPToolsPlugin : BaseUnityPlugin
    {
        private const string HarmonyId = "Satroki.DSP.plugin.DSPTools";

        public void Awake()
        {
            LoadConfig.Init(Config);

            // 每个功能独立注册，配置里关掉就不打补丁
            if (LoadConfig.EnableTank.Value)
                TryRegister(typeof(TankGameTickPatch), TankGameTickPatch.HarmonyId);
            if (LoadConfig.EnableAchievements.Value)
            {
                TryRegister(typeof(AchievementActivePatch), AchievementFeature.HarmonyId);
                TryRegister(typeof(AbnormalityGameTickPatch), AchievementFeature.HarmonyId);
                TryRegister(typeof(AbnormalityTriggerPatch), AchievementFeature.HarmonyId);
                TryRegister(typeof(AbnormalityImportPatch), AchievementFeature.HarmonyId);
            }
            if (LoadConfig.InfiniteSank.Value)
                TryRegister(typeof(SandPatch), HarmonyId + ".sand");
            if (LoadConfig.FreeStuff.Value)
                TryRegister(typeof(CargoTrafficPatch), HarmonyId + ".splitter");
            if (LoadConfig.FreeBuyTech.Value)
                TryRegister(typeof(GameHistoryDataPatch), HarmonyId + ".tech");

            // 分辨率补丁必须尽早注册：GameResolutions.InitResolutions() 在 DSPGame.Awake 里就被调用，
            // 晚于它就只能靠 GameOption.Apply() 的 prefix 兜底了
            if (LoadConfig.EnableCustomResolution.Value)
            {
                TryRegister(typeof(GameResolutionsInitPatch), HarmonyId + ".resolution");
                TryRegister(typeof(GameOptionApplyPatch), HarmonyId + ".resolution");
            }
        }

        private void TryRegister(Type patchType, string harmonyId)
        {
            try
            {
                Harmony.CreateAndPatchAll(patchType, harmonyId);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"补丁 {patchType.Name} 注册失败: " + ex);
            }
        }
    }
}
