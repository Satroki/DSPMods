using HarmonyLib;
using UnityEngine.Playables;
using UnityEngine;
using Steamworks;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Linq;


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

    [HarmonyPatch(typeof(GameHistoryData), "BuyoutTech")]
    public class GameHistoryDataPatch
    {
        [HarmonyPrefix]
        public static bool GameHistoryData_BuyoutTech_Prefix(GameHistoryData __instance, int techId, ref bool __result)
        {
            __result = false;
            if (!__instance.techStates.ContainsKey(techId))
            {
                return false;
            }

            TechProto techProto = LDB.techs.Select(techId);
            TechState techState = __instance.TechState(techId);
            PropertySystem propertySystem = DSPGame.propertySystem;
            Player mainPlayer = GameMain.mainPlayer;
            if (techProto == null || propertySystem == null || mainPlayer == null)
            {
                return false;
            }

            if (techState.hashUploaded >= techState.hashNeeded)
            {
                return false;
            }

            if (!__instance.CheckPropertyAdequateForBuyout(techId) || !__instance.HasPreTechUnlocked(techId))
            {
                return false;
            }
            __instance.UnlockTechUnlimited(techId, unlockedDirect: false);
            __result = true;
            return false;
        }
    }
}
