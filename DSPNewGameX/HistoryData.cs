using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;
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

namespace DSPNewGamePlus
{
    internal class HistoryData
    {

        public static void Export(BinaryWriter w)
        {
            var history = GameMain.history;


            w.Write(7);
            w.Write(history.recipeUnlocked.Count);
            //レシピ
            foreach (int value in history.recipeUnlocked)
            {
                w.Write(value);
            }
            //チュートリアル
            w.Write(history.tutorialUnlocked.Count);
            foreach (int value2 in history.tutorialUnlocked)
            {
                w.Write(value2);
            }
            //星のピンデータ
            //w.Write(history.featureKeys.Count);
            //foreach (int value3 in history.featureKeys)
            //{
            //    w.Write(value3);
            //}
            //featureValues
            //w.Write(history.featureValues.Count);
            //foreach (KeyValuePair<int, int> keyValuePair in history.featureValues)
            //{
            //    w.Write(keyValuePair.Key);
            //    w.Write(keyValuePair);
            //}
            //マイルストーン
            history.journalSystem.Export(w);
            w.Write(history.techStates.Count);
            //研究解除情報
            foreach (KeyValuePair<int, TechState> keyValuePair2 in history.techStates)
            {
                w.Write(keyValuePair2.Key);
                w.Write(keyValuePair2.Value.unlocked);
                w.Write(keyValuePair2.Value.curLevel);
                w.Write(keyValuePair2.Value.maxLevel);
                w.Write(keyValuePair2.Value.hashUploaded);
                w.Write(keyValuePair2.Value.hashNeeded);
            }
            //w.Write(history.autoManageLabItems); 
            //w.Write(history.currentTech);
            //w.Write(history.techQueue.Length);
            //for (int i = 0; i < history.techQueue.Length; i++)
            //{
            //	w.Write(history.techQueue[i]);
            //}
            w.Write(history.universeObserveLevel);
            w.Write(history.blueprintLimit);
            w.Write(history.solarSailLife);
            w.Write(history.solarEnergyLossRate);
            w.Write(history.useIonLayer);
            w.Write(history.inserterStackCount);
            w.Write(history.logisticDroneSpeed);
            w.Write(history.logisticDroneSpeedScale);
            w.Write(history.logisticDroneCarries);
            w.Write(history.logisticShipSailSpeed);
            w.Write(history.logisticShipWarpSpeed);
            w.Write(history.logisticShipSpeedScale);
            w.Write(history.logisticShipWarpDrive);
            w.Write(history.logisticShipCarries);
            w.Write(history.miningCostRate);
            w.Write(history.miningSpeedScale);
            w.Write(history.storageLevel);
            w.Write(history.labLevel);
            w.Write(history.techSpeed);
            w.Write(history.dysonNodeLatitude);
            w.Write(history.universeMatrixPointUploaded);
            w.Write(history.missionAccomplished);
            w.Write(history.stationPilerLevel);
            w.Write(history.remoteStationExtraStorage);
            w.Write(history.localStationExtraStorage);

        }

        public static void Import(BinaryReader r)
        {
            var history = GameMain.history;

            int num = r.ReadInt32();
            if (num == 7)
            {
                LogManager.Logger.LogInfo("-------------------------------------history.inport start");
            }
            else
            {
                LogManager.Logger.LogInfo("-------------------------------------history.inport error");

            }
            //レシピ
            int num2 = r.ReadInt32();
            for (int i = 0; i < num2; i++)
            {
                int item = r.ReadInt32();
                if (UI.TechnologiesEnable) //レシピを読み込むなら
                {
                    history.recipeUnlocked.Add(item);
                }
            }
            //if (num >= 2)
            //{
            //チュートリアル
            //LogManager.Logger.LogInfo("-------------------------------------history.inport チュートリアル");
            int num3 = r.ReadInt32();
            for (int j = 0; j < num3; j++)
            {
                int item2 = r.ReadInt32();
                if (UI.TutorialEnable) //チュートリアルを読み込むなら
                {
                    history.tutorialUnlocked.Add(item2);
                }
            }
            //}
            //惑星のピンデータ
            //int num4 = r.ReadInt32();
            //for (int k = 0; k < num4; k++)
            //{
            //    int num5 = r.ReadInt32();
            //    history.featureKeys.Add(num5);
            //    LogManager.Logger.LogInfo($"-------------------------------featureKeys-----key : {num5}");

            //    if (num5 > 1020000 && num5 < 1520000 && num5 < 1510000)
            //    {
            //        if (num < 3)
            //        {
            //            history.featureKeys.Remove(num5);
            //        }
            //        else
            //        {
            //            history.pinnedPlanets[num5 - 1020000] = num5 - 1020000;
            //        }
            //    }
            //}

            //featureValues
            //if (num >= 5)
            //{
            //    int num44 = r.ReadInt32();
            //    for (int l = 0; l < num44; l++)
            //    {
            //        int key = r.ReadInt32();
            //        int value = r.ReadInt32();
            //        history.featureValues[key] = value;
            //        LogManager.Logger.LogInfo($"-------------------------------featureValue-----key : {key}  value : {value}");
            //    }
            //}
            //else
            //{
            //    history.featureValues.Clear();
            //}

            //if (num >= 6)
            //{
            if (UI.MilestoneEnable) //マイルストーンを読み込むなら
            {
                history.journalSystem.Import(r);
            }
            else
            {
                r.ReadInt32();
                int num9 = r.ReadInt32();
                for (int i = 0; i < num9; i++)
                {
                    JournalData journalData = new JournalData();
                    journalData.Import(r);
                }
            }
            //}
            //else
            //{
            //	history.journalSystem.SetForNewGame();
            //}
            //LogManager.Logger.LogInfo("-------------------------------------history.inport techState");

            //技術研究＆性能強化
            history.techStates = new Dictionary<int, TechState>();

            int num6 = r.ReadInt32();
            //LogManager.Logger.LogInfo($" num6[{num6}]  ");
            for (int m = 0; m < num6; m++)
            {
                int num7 = r.ReadInt32();
                TechProto techProto = LDB.techs.Select(num7);
                TechState techState = default(TechState);
                techState.unlocked = r.ReadBoolean();
                techState.curLevel = r.ReadInt32();
                techState.maxLevel = r.ReadInt32();
                techState.hashUploaded = r.ReadInt64();
                techState.hashNeeded = r.ReadInt64();

                if (techProto != null)
                {
                    if (history.techStates.ContainsKey(num7))
                    {
                        history.techStates.Remove(num7);
                    }


                    LogManager.Logger.LogInfo($"Contain [{history.techStates.ContainsKey(num7)}]  Name[{techProto.Name.Translate()}]    Key[{num7}]  unlocked[{techState.unlocked}]  ");


                    //techState.hashNeeded = techProto.GetHashNeeded(techState.curLevel);
                    //	if (techProto.Items.Length != 0 && techProto.Items[0] == 6006)
                    //	{
                    //		techState.uPointPerHash = techProto.ItemPoints[0];
                    //	}
                    if (UI.TechnologiesEnable && techProto.ID < 2000 || UI.UpgradesEnable && techProto.ID > 2000) //レシピorアップグレードを読み込むなら
                    {
                        techState.hashNeeded = techProto.GetHashNeeded(techState.curLevel);
                        if (techProto.Items.Length != 0 && techProto.Items[0] == 6006)
                        {
                            techState.uPointPerHash = techProto.ItemPoints[0];
                        }
                        history.techStates.Add(num7, techState);
                    }
                    //else
                    //{
                    //    int upoint = 0;
                    //    if (techProto.Items.Length != 0 && techProto.Items[0] == 6006)
                    //    {
                    //        upoint = techProto.ItemPoints[0];
                    //    }
                    //    TechState value = new TechState(false, techProto.Level, techProto.MaxLevel, 0L, techProto.GetHashNeeded(techProto.Level), upoint);
                    //    history.techStates.Add(num7, value);
                    //}

                }
            }

            //読み込んでないdatestateは初期化
            TechProto[] dataArray = LDB.techs.dataArray;
            for (int n = 0; n < dataArray.Length; n++)
            {
                if (!history.techStates.ContainsKey(dataArray[n].ID))
                {
                    int upoint = 0;
                    if (dataArray[n].Items.Length != 0 && dataArray[n].Items[0] == 6006)
                    {
                        upoint = dataArray[n].ItemPoints[0];
                    }
                    TechState value2 = new TechState(false, dataArray[n].Level, dataArray[n].MaxLevel, 0L, dataArray[n].GetHashNeeded(dataArray[n].Level), upoint);
                    history.techStates.Add(dataArray[n].ID, value2);
                }
            }

            //ダイソンスフィアプログラムのアンロック
            TechState techState2 = default(TechState);
            techState2.unlocked = true;
            techState2.curLevel = 0;
            techState2.maxLevel = 0;
            techState2.hashUploaded = 1;
            techState2.hashNeeded = 1;
            if (history.techStates.ContainsKey(1))
            {
                history.techStates.Remove(1);
            }
            history.techStates.Add(1, techState2);

            //history.autoManageLabItems = r.ReadBoolean();
            //history.currentTech = r.ReadInt32();
            //if (!history.techStates.ContainsKey(history.currentTech))
            //{
            //	history.currentTech = 0;
            //}
            //if (num >= 1)
            //{
            //	int num8 = r.ReadInt32();
            //	history.techQueue = new int[num8];
            //	int num9 = 0;
            //	for (int num10 = 0; num10 < num8; num10++)
            //	{
            //		int num11 = r.ReadInt32();
            //		if (history.techStates.ContainsKey(num11))
            //		{
            //			history.techQueue[num9++] = num11;
            //		}
            //	}
            //}
            //else
            //{
            //	history.techQueue = new int[8];
            //	history.techQueue[0] = history.currentTech;
            //}
            //LogManager.Logger.LogInfo("-------------------------------------history.inport Upgrades");

            if (UI.UpgradesEnable)
            {
                history.universeObserveLevel = r.ReadInt32();
                //if (num >= 4)
                //{
                history.blueprintLimit = r.ReadInt32();
                //}
                //else
                //{
                //	history.blueprintLimit = Configs.freeMode.blueprintLimit;
                //}
                history.solarSailLife = r.ReadSingle();
                history.solarEnergyLossRate = r.ReadSingle();
                history.useIonLayer = r.ReadBoolean();
                history.inserterStackCount = r.ReadInt32();
                history.logisticDroneSpeed = r.ReadSingle();
                history.logisticDroneSpeedScale = r.ReadSingle();
                history.logisticDroneCarries = r.ReadInt32();
                history.logisticShipSailSpeed = r.ReadSingle();
                history.logisticShipWarpSpeed = r.ReadSingle();
                history.logisticShipSpeedScale = r.ReadSingle();
                history.logisticShipWarpDrive = r.ReadBoolean();
                history.logisticShipCarries = r.ReadInt32();
                history.miningCostRate = r.ReadSingle();
                history.miningSpeedScale = r.ReadSingle();
                history.storageLevel = r.ReadInt32();
                history.labLevel = r.ReadInt32();
                history.techSpeed = r.ReadInt32();
                history.dysonNodeLatitude = r.ReadSingle();
                history.universeMatrixPointUploaded = r.ReadInt64();
                history.missionAccomplished = r.ReadBoolean();
                //if (history.gameData.patch < 3 && history.techStates[1602].unlocked)
                //{
                //    history.UnlockRecipe(117);
                //}
                //if (num >= 7)
                //{
                history.stationPilerLevel = r.ReadInt32();
                history.remoteStationExtraStorage = r.ReadInt32();
                history.localStationExtraStorage = r.ReadInt32();
                //return;
                //}
                //history.stationPilerLevel = 1;
                //history.remoteStationExtraStorage = 0;
                //history.localStationExtraStorage = 0;

            }
            else
            {
                r.ReadInt32();
                r.ReadInt32();
                r.ReadSingle();
                r.ReadSingle();
                r.ReadBoolean();
                r.ReadInt32();
                r.ReadSingle();
                r.ReadSingle();
                r.ReadInt32();
                r.ReadSingle();
                r.ReadSingle();
                r.ReadSingle();
                r.ReadBoolean();
                r.ReadInt32();
                r.ReadSingle();
                r.ReadSingle();
                r.ReadInt32();
                r.ReadInt32();
                r.ReadInt32();
                r.ReadSingle();
                r.ReadInt64();
                r.ReadBoolean();
                r.ReadInt32();
                r.ReadInt32();
                r.ReadInt32();
            }
        }
    }
}
