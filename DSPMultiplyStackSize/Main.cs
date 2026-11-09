using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace DSPMultiplyStackSize
{
    [BepInPlugin("Appun.DSP.plugin.MultiplyStackSize", "DSPMultiplyStackSize", "1.0.13")]
    public class Main : BaseUnityPlugin
    {
        public static bool StackSizeUpdated = false;

        public static Sprite stackIcon;

        public static ConfigEntry<int> configMultiplyStackSize;

        public static ConfigEntry<bool> editWarperSlotStackSize;

        public static bool stackSizeChangerMode = false;

        public static int maxIndex;

        public static Dictionary<int, int> stackSizeDictionaryOrigin = new Dictionary<int, int>();

        public static Dictionary<int, int> stackSizeDictionary = new Dictionary<int, int>();

        public static string PluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public static string jsonFilePath;

        public void Start()
        {
            configMultiplyStackSize = Config.Bind("General", "StackSizeMultiplier", 5, "Multiplies the stack size of all items.");
            editWarperSlotStackSize = Config.Bind("General", "changeWarperSlotStackSize", true, "change Mecha Warper Slot Stack Size ");
            LogManager.Logger = Logger;

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);

            jsonFilePath = Path.Combine(Paths.ConfigPath, "stackSizeDictionary.json");
            if (!File.Exists(jsonFilePath))
            {
                File.WriteAllText(jsonFilePath, "{}");
            }
            else
            {
                try
                {
                    stackSizeDictionary = StackSizeJson.FromJson(File.ReadAllText(jsonFilePath));
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("Failed to read stackSizeDictionary.json: " + ex);
                    stackSizeDictionary = new Dictionary<int, int>();
                }
            }

            LoadIcon();
            maxIndex = 112;

            try
            {
                UI.Create();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to create stack size editor UI: " + ex);
            }
        }

        public static void LoadIcon()
        {
            try
            {
                AssetBundle val = AssetBundle.LoadFromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("DSPMultiplyStackSize.multiplystacksize"));
                if (val == null)
                {
                    LogManager.Logger.LogInfo("Icon bundle not found.");
                    return;
                }
                stackIcon = val.LoadAsset<Sprite>("StackSize");
                val.Unload(false);
            }
            catch (Exception ex)
            {
                LogManager.Logger.LogInfo("e.Message " + ex.Message);
                LogManager.Logger.LogInfo("e.StackTrace " + ex.StackTrace);
            }
        }
    }
}
