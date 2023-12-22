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


[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace DSPNewGamePlus
{

    [BepInPlugin("Appun.DSP.plugin.NewGamePlus", "DSPNewGamePlus", "1.1.3")]
    [BepInProcess("DSPGAME.exe")]

    public class Main : BaseUnityPlugin
    {
        public static ConfigEntry<KeyCode> openKey;
        //public static ConfigEntry<KeyCode> KeyConfig2;
        public static string PluginPath = System.IO.Path.GetDirectoryName(
                   System.Reflection.Assembly.GetExecutingAssembly().Location);

        public void Start()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

            LogManager.Logger = Logger;

            UI.Create();

        }


        //
        public static void Save()　　//SaveCurrentGame(string saveName)
        {
            LogManager.Logger.LogInfo("save");
            string path = Path.Combine(PluginPath, "DSPNewGamePlus.save");
            FileStream fileStream = new FileStream(path, FileMode.Create);
            BinaryWriter binaryWriter = new BinaryWriter(fileStream);

            //インベントリ
            PlayerData.Export(binaryWriter);
            //ヒストリ
            HistoryData.Export(binaryWriter);
            //メカ
            MechaData.Export(binaryWriter);

            binaryWriter.Close(); 
            fileStream.Close();
            UI.openedMessageBox = false;

        }

        public static void Load()
        {
            //読み込み
            var Data = GameMain.data;
            LogManager.Logger.LogInfo("load");
            string path = Path.Combine(PluginPath, "DSPNewGamePlus.save");
            if (!File.Exists(path))
            {
                LogManager.Logger.LogInfo("DSPNewGamePlus.save not found " + path);
                UI.openedMessageBox = false;

                return;
            }
            FileStream fileStream = new FileStream(path, FileMode.Open);

            BinaryReader BinaryReader = new BinaryReader(fileStream);
            Data.history.techStates.Clear();

            //インベントリ
            PlayerData.Import(BinaryReader);
            //ヒストリ
            HistoryData.Import(BinaryReader);
            //メカ
            MechaData.Import(BinaryReader);


            BinaryReader.Close();
            fileStream.Close();

            UI.openedMessageBox = false;
        }

        void Update()
        {
            if (UI.openedMessageBox)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    UI.openedMessageBox = false;
                    UI.messageBox.FadeOut();
                }
            }
            //if (Input.GetKeyDown(KeyCode.F1))
            //{

            //    var writer = new System.IO.StreamWriter("test.txt");
            //    foreach (KeyValuePair<int, TechState> keyValuePair2 in GameMain.history.techStates)
            //    {
            //        //w.Write(keyValuePair2.Key);
            //        //w.Write(keyValuePair2.Value.unlocked);
            //        //w.Write(keyValuePair2.Value.curLevel);
            //        //w.Write(keyValuePair2.Value.maxLevel);
            //        //w.Write(keyValuePair2.Value.hashUploaded);
            //        //w.Write(keyValuePair2.Value.hashNeeded);

            //        TechProto techProto = LDB.techs.Select(keyValuePair2.Key);
            //        //if (techProto != null)
            //        //{

            //        //    LogManager.Logger.LogInfo($"  Name[{techProto.Name.Translate()}]    Key[{keyValuePair2.Key}]  unlocked[{keyValuePair2.Value.unlocked}]  ");
            //        //    writer.WriteLine(techProto.Name.Translate() + "," + keyValuePair2.Key + "," + keyValuePair2.Value.unlocked + "," + keyValuePair2.Value.curLevel + "," + keyValuePair2.Value.maxLevel + "," + keyValuePair2.Value.hashUploaded + "," + keyValuePair2.Value.hashNeeded);
            //        //}
            //        //else
            //        //{
            //        LogManager.Logger.LogInfo($" Key[{keyValuePair2.Key}]  unlocked[{keyValuePair2.Value.unlocked}]  ");
            //        writer.WriteLine(keyValuePair2.Key + "," + keyValuePair2.Value.unlocked + "," + keyValuePair2.Value.curLevel + "," + keyValuePair2.Value.maxLevel + "," + keyValuePair2.Value.hashUploaded + "," + keyValuePair2.Value.hashNeeded);

            //        //}
            //    }
            //    writer.Close();

            //}



        }


        public static void Cancel()
        {
            UI.openedMessageBox = false;
        }


    }


    public class LogManager
    {
        public static ManualLogSource Logger;
    }
}

