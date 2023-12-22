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

    public class UI : MonoBehaviour
    {
        public static UIMessageBox messageBox;
        public static bool openedMessageBox = false;
        public static GameObject newGamePlusButton;
        public static Sprite iconSprite;

        public static GameObject checkBox1;
        public static GameObject checkBox2;
        public static GameObject checkBox3;
        public static GameObject checkBox4;
        public static GameObject checkBox5;
        public static GameObject checkBox6;
        public static GameObject checkBoxTitle;
        public static GameObject CautionText;


        public static bool MilestoneEnable = true;
        public static bool TutorialEnable = true;
        public static bool FuelChamberEnable = true;
        public static bool InventoryEnable = true;
        public static bool TechnologiesEnable = true;
        public static bool UpgradesEnable = true;

        public static void Create()
        {

            //ボタンの作成
            newGamePlusButton = Instantiate(GameObject.Find("UI Root/Overlay Canvas/In Game/Esc Menu/button (6)"), GameObject.Find("UI Root/Overlay Canvas/In Game/Esc Menu").transform) as GameObject;
            newGamePlusButton.transform.localPosition = new Vector3(0, -325, 0);
            newGamePlusButton.GetComponentInChildren<Text>().text = "NewGame+".Translate();
            Destroy(newGamePlusButton.transform.Find("Text").GetComponent<Localizer>());
            Destroy(newGamePlusButton.GetComponent<UIButton>());
            newGamePlusButton.GetComponent<Image>().color = new Color(0.4f, 0.8f, 1, 1);


            newGamePlusButton.SetActive(true);

            //ボタンクリックイベントの追加
            newGamePlusButton.GetComponent<Button>().onClick.AddListener(OnClicknewGamePlusButton);


            iconSprite = Resources.Load<Sprite>("Icons/Tech/1");


        }

        public static void OnClicknewGamePlusButton()
        {

        MilestoneEnable = true;
        TutorialEnable = true;
        FuelChamberEnable = true;
        InventoryEnable = true;
        TechnologiesEnable = true;
        UpgradesEnable = true;


        openedMessageBox = true;
            var Data = GameMain.data;


            string path = Path.Combine(Main.PluginPath, "DSPNewGamePlus.save");
            LogManager.Logger.LogInfo("path : " + path);

            if (File.Exists(path))
            {
                messageBox = UIMessageBox.Show(
                    "NewGame+".Translate(),
                    "You can save and load Mecha,Techs,Resipes and items stored in your inventory.".Translate(),
                    "Cancel".Translate(),
                    "Load".Translate(),
                    "Save".Translate(),
                    0,
                    new UIMessageBox.Response(Main.Cancel),
                    new UIMessageBox.Response(Main.Load),
                    new UIMessageBox.Response(Main.Save)
                );
            }
            else
            {
                messageBox = UIMessageBox.Show(
                    "NewGame+".Translate(),
                    "You can save and load Mecha,Techs,Resipes and items stored in your inventory.".Translate(),
                    "Cancel".Translate(),
                    "Save".Translate(),
                    0,
                    new UIMessageBox.Response(Main.Cancel),
                    new UIMessageBox.Response(Main.Save)
                );
            }

            messageBox.m_WindowTrans.sizeDelta = new Vector2(600, 440);
            messageBox.m_IconImage.sprite = iconSprite;
            messageBox.m_IconImage.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 80);
            messageBox.m_IconImage.transform.localPosition = new Vector3(-270, 182, 0);
            messageBox.m_IconImage.color = new Color(0f, 1f, 1f, 1f);

            checkBoxTitle = Instantiate(GameObject.Find("UI Root/Overlay Canvas/DialogGroup/MessageBox VE(Clone)/Window/Body/Client/Text"), messageBox.gameObject.transform.Find("Window/Body")) as GameObject;
            checkBoxTitle.transform.localPosition = new Vector3(-170, 115, 0);
            checkBoxTitle.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 80);
            checkBoxTitle.GetComponent<Text>().text = "Load Option".Translate();
            checkBoxTitle.GetComponent<Text>().alignment = TextAnchor.MiddleRight;

            checkBox1 = Instantiate(GameObject.Find("UI Root/Overlay Canvas/In Game/Function Panel/Build Menu/ux-group/checkbox-facilities"), messageBox.gameObject.transform.Find("Window/Body")) as GameObject;
            checkBox1.transform.localPosition = new Vector3(30, 90, 0);
            checkBox1.transform.Find("text").GetComponent<Text>().text = "Milestone".Translate();
            checkBox1.transform.Find("text").GetComponent<Text>().fontSize = 16;
            Destroy(checkBox1.transform.Find("text").GetComponent<Localizer>());
            checkBox1.transform.Find("checked").GetComponent<Image>().enabled = true;
            checkBox1.AddComponent<Button>();
            checkBox1.transform.Find("checked").gameObject.SetActive(true);

            checkBox2 = Instantiate(checkBox1.gameObject, checkBox1.transform.parent) as GameObject;
            checkBox2.transform.localPosition = new Vector3(30, 75, 0);
            checkBox2.transform.Find("text").GetComponent<Text>().text = "Tutorial".Translate();

            checkBox3 = Instantiate(checkBox1.gameObject, checkBox1.transform.parent) as GameObject;
            checkBox3.transform.localPosition = new Vector3(30, 45, 0);
            checkBox3.transform.Find("text").GetComponent<Text>().text = "FuelChamber&WarperSlot".Translate();

            checkBox4 = Instantiate(checkBox1.gameObject, checkBox1.transform.parent) as GameObject;
            checkBox4.transform.localPosition = new Vector3(30, 15, 0);
            checkBox4.transform.Find("text").GetComponent<Text>().text = "Inventory&SoilPile".Translate();

            checkBox5 = Instantiate(checkBox1.gameObject, checkBox1.transform.parent) as GameObject;
            checkBox5.transform.localPosition = new Vector3(30, -15, 0);
            checkBox5.transform.Find("text").GetComponent<Text>().text = "Technologies&Recipes".Translate();

            checkBox6 = Instantiate(checkBox1.gameObject, checkBox1.transform.parent) as GameObject;
            checkBox6.transform.localPosition = new Vector3(30, -45, 0);
            checkBox6.transform.Find("text").GetComponent<Text>().text = "Upgrades&MechaPerformance".Translate();

            CautionText = Instantiate(checkBoxTitle.gameObject, checkBoxTitle.transform.parent) as GameObject;
            CautionText.transform.localPosition = new Vector3(-170, -80, 0);
            CautionText.GetComponent<RectTransform>().sizeDelta = new Vector2(420, 80);
            CautionText.GetComponent<Text>().text = "Loading only one of  [Technologies&Recipes] or [Upgrades&MechaPerformance] will inconsistent and affect achievements until the requested research is unlocked.".Translate();
            CautionText.GetComponent<Text>().color = new Color(1f, 0.7f, 0f, 0.7f);
            CautionText.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;



            //ボタンクリックイベントの追加
            //checkBox1.GetComponent<Button>().onClick.AddListener(OnClickCheckBox1);
            checkBox2.GetComponent<Button>().onClick.AddListener(OnClickCheckBox2);
            checkBox3.GetComponent<Button>().onClick.AddListener(OnClickCheckBox3);
            checkBox4.GetComponent<Button>().onClick.AddListener(OnClickCheckBox4);
            checkBox5.GetComponent<Button>().onClick.AddListener(OnClickCheckBox5);
            checkBox6.GetComponent<Button>().onClick.AddListener(OnClickCheckBox6);


            checkBox1.gameObject.SetActive(false);
        }



        //public static void OnClickCheckBox1()
        //{
        //    MilestoneEnable = !MilestoneEnable;
        //    checkBox1.transform.Find("checked").gameObject.SetActive(MilestoneEnable);
        //    LogManager.Logger.LogInfo("-------------------------------------MilestoneEnable " + MilestoneEnable);
        //}

        public static void OnClickCheckBox2()
        {
            TutorialEnable = !TutorialEnable;
            checkBox2.transform.Find("checked").gameObject.SetActive(TutorialEnable);
            LogManager.Logger.LogInfo("-------------------------------------TutorialEnable " + TutorialEnable);
        }

        public static void OnClickCheckBox3()
        {
            FuelChamberEnable = !FuelChamberEnable;
            checkBox3.transform.Find("checked").gameObject.SetActive(FuelChamberEnable);
            LogManager.Logger.LogInfo("-------------------------------------FuelChamberEnable " + FuelChamberEnable);
        }

        public static void OnClickCheckBox4()
        {
            InventoryEnable = !InventoryEnable;
            checkBox4.transform.Find("checked").gameObject.SetActive(InventoryEnable);
            LogManager.Logger.LogInfo("-------------------------------------InventoryEnable " + InventoryEnable);
        }

        public static void OnClickCheckBox5()
        {
            TechnologiesEnable = !TechnologiesEnable;
            checkBox5.transform.Find("checked").gameObject.SetActive(TechnologiesEnable);
            LogManager.Logger.LogInfo("-------------------------------------TechnologiesEnable " + TechnologiesEnable);
        }
        public static void OnClickCheckBox6()
        {
            UpgradesEnable = !UpgradesEnable;
            checkBox6.transform.Find("checked").gameObject.SetActive(UpgradesEnable);
            LogManager.Logger.LogInfo("-------------------------------------UpgradesEnable " + UpgradesEnable);
        }


    }
}
