using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Events;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System;
using System.IO;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using Newtonsoft.Json.Linq;

namespace DSPNewGamePlus
{
    public class HistoryData
    {

        public static void Export(History w)
        {
            var history = GameMain.history;
            w.techDict = history.techStates;
            w.universeMatrixPointUploaded = history.universeMatrixPointUploaded;
        }

        public static void Import(History r)
        {
            var history = GameMain.history;
            LogManager.Logger.LogInfo("-------------------------------------history.inport start");

            r.techDict.Remove(1);
            history.techStates[1] = new TechState(true, 0, 0, 1, 1, 0);

            foreach (var kv in r.techDict.OrderBy(kv => kv.Key))
            {
                var key = kv.Key;
                var value = kv.Value;
                TechProto techProto = LDB.techs.Select(key);
                if (techProto == null)
                    continue;
                if ((techProto.ID < 2000 && !UI.TechnologiesEnable) || (!UI.UpgradesEnable && techProto.ID > 2000) || techProto.ID == 1508)
                {
                    value.unlocked = false;
                    value.curLevel = techProto.Level;
                    value.hashUploaded = 0;
                    value.hashNeeded = techProto.GetHashNeeded(techProto.Level);
                }
                history.techStates[key] = value;

                //LogManager.Logger.LogInfo($"{techProto.ID} {techProto.name} {techProto.Level} {techProto.MaxLevel}");

                if (value.unlocked || (value.curLevel > techProto.Level && value.curLevel <= techProto.MaxLevel))
                {
                    for (int i = 0; i < techProto.UnlockRecipes.Length; i++)
                    {
                        history.UnlockRecipe(techProto.UnlockRecipes[i]);
                    }
                    var curLevel = value.unlocked ? value.curLevel : value.curLevel - 1;
                    for (var level = techProto.Level; level <= curLevel; level++)
                    {
                        for (int j = 0; j < techProto.UnlockFunctions.Length; j++)
                        {
                            history.UnlockTechFunction(techProto.UnlockFunctions[j], techProto.UnlockValues[j], level);
                        }
                    }
                    history.RegFeatureKey(1000100);
                }
            }

            history.universeMatrixPointUploaded = r.universeMatrixPointUploaded;
        }
    }
}
