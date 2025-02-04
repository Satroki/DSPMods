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
using Newtonsoft.Json;

[module: UnverifiableCode]

namespace DSPNewGamePlus
{
    [BepInPlugin("Appun.DSP.plugin.NewGamePlus", "DSPNewGamePlus", "1.1.3")]
    [BepInProcess("DSPGAME.exe")]

    public class Main : BaseUnityPlugin
    {
        public static string PluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public static KeyboardShortcut Hotkey;

        public void Start()
        {
            Hotkey = new KeyboardShortcut(KeyCode.Alpha5, KeyCode.LeftAlt);

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
            LogManager.Logger = Logger;
        }

        public static void Save()
        {
            LogManager.Logger.LogInfo("save");
            string path = Path.Combine(PluginPath, "DSPNewGamePlus.json");
            var save = new SaveModel { History = new History() };
            HistoryData.Export(save.History);

            File.WriteAllText(path, JsonConvert.SerializeObject(save));

            UI.openedMessageBox = false;
        }

        public static void Load()
        {
            var Data = GameMain.data;
            LogManager.Logger.LogInfo("load");
            string path = Path.Combine(PluginPath, "DSPNewGamePlus.json");
            if (!File.Exists(path))
            {
                LogManager.Logger.LogInfo("DSPNewGamePlus.json not found " + path);
                UI.openedMessageBox = false;

                return;
            }
            var save = File.ReadAllText(path);
            var saveModel = JsonConvert.DeserializeObject<SaveModel>(save);

            Data.history.techStates.Clear();
           
            HistoryData.Import(saveModel.History);
         
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
            else if (Hotkey.IsDown())
            {
                UI.OnClicknewGamePlusButton();
            }
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

