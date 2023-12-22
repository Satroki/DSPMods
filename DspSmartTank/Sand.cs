using HarmonyLib;

namespace SmartTankS
{
    [HarmonyPatch(typeof(Player), "SetSandCount")]
    public class SandPatch
    {
        private static void Prefix(ref int newSandCount)
        {
            newSandCount = 10000000;
        }
    }

    [HarmonyPatch(typeof(CargoTraffic), "UpdateSplitter")]
    public class CargoTrafficPatch
    {
        [HarmonyPrefix]
        public static bool CargoTraffic_UpdateSplitter_Prefix(CargoTraffic __instance, ref SplitterComponent sp)
        {
            if (sp.topId == 0)
            {
                if (sp.input0 == 0 && sp.output0 != 0 && sp.outFilter != 0)
                {
                    var outputPath = __instance.GetCargoPath(__instance.beltPool[sp.output0].segPathId);
                    if (outputPath != null && outputPath.TestBlankAtHead() == 0)
                    {
                        outputPath.TryInsertItemAtHead(sp.outFilter, 4, 16);
                    }
                }
            }
            return true;
        }
    }
}
