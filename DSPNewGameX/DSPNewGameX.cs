using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using HarmonyLib;

namespace DSPNewGameX
{
    [BepInPlugin("Satroki.DSP.plugin.NewGameX", "DSPNewGameX", "1.0.0")]
    [BepInProcess("DSPGAME.exe")]
    public class DSPNewGameX : BaseUnityPlugin
    {
        public static ConfigEntry<KeyCode> openKey;

        public static string PluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public static GameData Data;

        public UIMessageBox messageBox;

        public static bool openedMessageBox = false;

        public static GameObject newGamePlusButton;

        public void Start()
        {
            LogManager.Logger = base.Logger;
            newGamePlusButton = Instantiate(GameObject.Find("UI Root/Overlay Canvas/In Game/Esc Menu/button (6)"), GameObject.Find("UI Root/Overlay Canvas/In Game/Esc Menu").transform);
            newGamePlusButton.transform.localPosition = new Vector3(0f, -325f, 0f);
            newGamePlusButton.GetComponentInChildren<Text>().text = "新游戏+".Translate();
            Destroy(newGamePlusButton.transform.Find("Text").GetComponentInChildren<Localizer>());
            Destroy(newGamePlusButton.GetComponentInChildren<UIButton>());
            newGamePlusButton.GetComponentInChildren<Image>().color = new Color(0.4f, 0.8f, 1f, 1f);
            newGamePlusButton.SetActive(value: true);
            newGamePlusButton.GetComponent<Button>().onClick.AddListener(OnClicknewGamePlusButton);

            LoadConfig.Init(Config);

            Harmony.CreateAndPatchAll(typeof(SmartTank));
            Harmony.CreateAndPatchAll(typeof(SandPatch));
        }

        public void OnClicknewGamePlusButton()
        {
            openedMessageBox = true;
            Data = GameMain.data;
            string path = Path.Combine(PluginPath, "DSPNewGameX.save");
            if (File.Exists(path))
            {
                messageBox = UIMessageBox.Show("新游戏+".Translate(), "保存或加载存档数据".Translate(), "取消".Translate(), "载入".Translate(), "保存".Translate(), 0, Cancel, FullLoad, Save);
            }
            else
            {
                messageBox = UIMessageBox.Show("新游戏+".Translate(), "保存当前存档数据".Translate(), "取消".Translate(), "保存".Translate(), 0, Cancel, Save);
            }
        }

        public void Update()
        {
            if (openedMessageBox && Input.GetKeyDown(KeyCode.Escape))
            {
                openedMessageBox = false;
                messageBox.FadeOut();
            }
        }

        public void Cancel()
        {
            openedMessageBox = false;
        }

        public void Save()
        {
            LogManager.Logger.LogInfo("save");
            string path = Path.Combine(PluginPath, "DSPNewGameX.save");
            FileStream fileStream = new FileStream(path, FileMode.Create);
            BinaryWriter binaryWriter = new BinaryWriter(fileStream);
            GameMain.mainPlayer.mecha.Export(binaryWriter);
            GameMain.mainPlayer.package.Export(binaryWriter);
            GameMain.history.Export(binaryWriter);
            binaryWriter.Close();
            fileStream.Close();
            openedMessageBox = false;
        }

        public void FullLoad()
        {
            Data = GameMain.data;
            LogManager.Logger.LogInfo("load");
            string text = Path.Combine(PluginPath, "DSPNewGameX.save");
            if (!File.Exists(text))
            {
                LogManager.Logger.LogInfo("DSPNewGameX.save not found " + text);
                openedMessageBox = false;
                return;
            }
            FileStream fileStream = new FileStream(text, FileMode.Open);
            BinaryReader binaryReader = new BinaryReader(fileStream);
            Data.history.techStates.Clear();
            Data.mainPlayer.mecha.Import(binaryReader);
            Data.mainPlayer.package.Import(binaryReader);
            Data.history.Import(binaryReader);
            if (!LoadConfig.LoadReactor.Value)
            {
                Data.mainPlayer.mecha.reactorStorage.Clear();
                Data.mainPlayer.mecha.reactorItemId = 0;
                Data.mainPlayer.mecha.warpStorage.Clear();
            }
            if (!LoadConfig.LoadPackage.Value)
            {
                Data.mainPlayer.package.Clear();
            }
            if (!LoadConfig.LoadTech.Value)
            {
                var techIds = Data.history.techStates.Keys.Where(k => k > 1000 && k < 2000).ToArray();
                foreach (var tid in techIds)
                {
                    ResetTech(tid);
                    TechProto techProto = LDB.techs.Select(tid);
                    if (techProto != null)
                    {
                        for (int i = 0; i < techProto.UnlockRecipes.Length; i++)
                        {
                            Data.history.recipeUnlocked.Remove(techProto.UnlockRecipes[i]);
                        }
                    }
                }
            }
            if (LoadConfig.NotClear.Value)
            {
                ResetTech(1508);
            }
            binaryReader.Close();
            fileStream.Close();
            StarData[] stars = GameMain.universeSimulator.galaxyData.stars;
            foreach (StarData starData in stars)
            {
                Data.history.UnregFeatureKey(1010000 + starData.id);
                Data.history.RegFeatureKey(1510000 + starData.id);
                PlanetData[] planets = starData.planets;
                foreach (PlanetData planetData in planets)
                {
                    Data.history.UnregFeatureKey(1020000 + planetData.id);
                    Data.history.RegFeatureKey(1520000 + planetData.id);
                }
            }
            openedMessageBox = false;
        }

        private void ResetTech(int tid)
        {
            var ts = Data.history.techStates[tid];
            ts.unlocked = false;
            ts.curLevel = 0;
            ts.hashUploaded = 0;
            Data.history.techStates[tid] = ts;
        }
    }
}


