using System;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DSPMultiplyStackSize
{
    public class UI : MonoBehaviour
    {
        public static UIMessageBox messageBox;

        public static GameObject selectedItem;

        public static GameObject stackSizeButton;

        public static GameObject ItemPicker;

        public static GameObject selectedItemStackSizeInput;

        public static GameObject selectedItemName;

        public static GameObject stackSizeLabel;

        public static GameObject applyButton;

        public static GameObject resetButton;

        public static GameObject numTextsBase;

        public static Text prefabNumText;

        public static Text[] numTexts;

        public static Color normalColor = new Color(0.4f, 0.8f, 1f, 0.8f);

        public static Color editedColor = new Color(1f, 0.6f, 0f, 0.9f);

        // The inventory-window button may be missing after a game update.
        // In that case F8 opens the same editor, so the mod stays usable.
        public static bool hotkeyFallback;

        public static void Create()
        {
            if (UIRoot.instance == null || UIRoot.instance.uiGame == null || UIRoot.instance.uiGame.itemPicker == null)
            {
                LogManager.Logger.LogError("Stack size editor: UI is not ready.");
                return;
            }

            GameObject driver = new GameObject("DSPMultiplyStackSize_UI");
            UnityEngine.Object.DontDestroyOnLoad(driver);
            driver.AddComponent<UI>();

            ItemPicker = UIRoot.instance.uiGame.itemPicker.gameObject;

            Transform content = ItemPicker.transform.Find("content");
            if (content == null)
            {
                content = ItemPicker.transform;
            }

            prefabNumText = CreateTextTemplate(content);
            if (prefabNumText == null)
            {
                LogManager.Logger.LogError("Stack size editor: no Text template found.");
                hotkeyFallback = true;
                return;
            }

            numTextsBase = new GameObject("numTextsBase");
            numTextsBase.transform.SetParent(content, false);
            numTextsBase.transform.localPosition = new Vector3(-273f, 5f, 0f);
            numTextsBase.SetActive(false);

            numTexts = new Text[Main.maxIndex];
            for (int i = 0; i < Main.maxIndex; i++)
            {
                numTexts[i] = UnityEngine.Object.Instantiate(prefabNumText, numTextsBase.transform);
                numTexts[i].name = "numTexts" + i;
                int column = i % 14;
                int row = i / 14;
                numTexts[i].rectTransform.anchoredPosition = new Vector2(column * 46 - 5 - 46, row * -46 - 29);
            }

            selectedItem = new GameObject("selectedItem");
            selectedItem.transform.SetParent(ItemPicker.transform, false);
            selectedItem.transform.localPosition = new Vector3(-55f, -5f, 0f);
            selectedItem.SetActive(false);

            selectedItemStackSizeInput = CreateInputField(selectedItem.transform);
            selectedItemName = CreateLabel(selectedItem.transform, "selectedItemName", "", 16, new Vector3(445f, -60f, 0f));
            stackSizeLabel = CreateLabel(selectedItem.transform, "stackSizeLabel", "Stack Size".Translate(), 14, new Vector3(385f, -87f, 0f));

            applyButton = CreateButton(selectedItem.transform, "applyButton", "Apply".Translate(), new Vector3(565f, -87f, 0f), new Vector2(60f, 26f), new Color(0.24f, 0.55f, 0.65f, 0.7f));
            if (applyButton != null)
            {
                Button apply = applyButton.GetComponent<Button>();
                if (apply != null)
                {
                    apply.onClick.AddListener(new UnityAction(OnClickApplyButton));
                }

                resetButton = UnityEngine.Object.Instantiate(applyButton, ItemPicker.transform);
                resetButton.name = "resetButton";
                resetButton.transform.localPosition = new Vector3(580f, -105f, 0f);
                RectTransform resetRect = resetButton.GetComponent<RectTransform>();
                if (resetRect != null)
                {
                    resetRect.sizeDelta = new Vector2(40f, 20f);
                }
                Text resetText = resetButton.GetComponentInChildren<Text>();
                if (resetText != null)
                {
                    resetText.text = "Reset".Translate();
                }
                Image resetImage = resetButton.GetComponent<Image>();
                if (resetImage != null)
                {
                    resetImage.color = new Color(1f, 0.68f, 0.45f, 0.7f);
                }
                Button reset = resetButton.GetComponent<Button>();
                if (reset != null)
                {
                    reset.onClick.AddListener(new UnityAction(OnClickResetButton));
                }
                resetButton.SetActive(true);
            }

            CreateStackSizeButton();
            RefreshNumTexts();
            LogManager.Logger.LogInfo("Stack size editor ready: " + (stackSizeButton != null ? "inventory button created" : "F8 hotkey only"));
        }

        private void Update()
        {
            if (hotkeyFallback && Input.GetKeyDown(KeyCode.F8))
            {
                OnClickStackSizeButton();
            }
        }

        private static Text CreateTextTemplate(Transform parent)
        {
            Text template = null;

            UIInventoryWindow inventoryWindow = UIRoot.instance.uiGame.inventoryWindow;
            if (inventoryWindow != null && inventoryWindow.inventory != null)
            {
                template = inventoryWindow.inventory.prefabNumText;
            }

            if (template == null && UIRoot.instance.uiGame.replicator != null)
            {
                template = UIRoot.instance.uiGame.replicator.prefabNumText;
            }

            if (template == null)
            {
                template = ItemPicker.GetComponentInChildren<Text>(true);
            }

            if (template == null)
            {
                return null;
            }

            Text text = UnityEngine.Object.Instantiate(template, parent);
            text.fontSize = 14;

            Outline outline = text.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectDistance = new Vector2(2f, 2f);
                outline.enabled = true;
            }

            Shadow shadow = text.GetComponent<Shadow>();
            if (shadow != null)
            {
                shadow.effectDistance = new Vector2(2f, 2f);
                shadow.enabled = true;
            }

            text.rectTransform.sizeDelta = new Vector2(46f, 24f);
            return text;
        }

        private static GameObject CreateLabel(Transform parent, string name, string value, int fontSize, Vector3 position)
        {
            GameObject label = UnityEngine.Object.Instantiate(prefabNumText.gameObject, parent);
            label.name = name;
            label.transform.localPosition = position;

            Text text = label.GetComponent<Text>();
            if (text != null)
            {
                text.text = value;
                text.fontSize = fontSize;
                text.alignment = TextAnchor.MiddleLeft;
            }

            return label;
        }

        private static GameObject CreateInputField(Transform parent)
        {
            InputField template = null;

            UIBlueprintBrowser browser = UIRoot.instance.uiGame.blueprintBrowser;
            if (browser != null && browser.inspector != null)
            {
                template = browser.inspector.shortTextInput;
            }

            if (template == null)
            {
                template = UnityEngine.Object.FindObjectOfType<InputField>();
            }

            if (template == null)
            {
                LogManager.Logger.LogWarning("Stack size editor: no InputField template found.");
                return null;
            }

            GameObject inputObject = UnityEngine.Object.Instantiate(template.gameObject, parent);
            inputObject.name = "inputText";
            inputObject.transform.localPosition = new Vector3(440f, -75f, 0f);

            InputField input = inputObject.GetComponent<InputField>();
            if (input != null)
            {
                input.contentType = InputField.ContentType.IntegerNumber;
                input.characterLimit = 6;
            }

            RectTransform rect = inputObject.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(60f, 24f);
            }

            UIButton button = inputObject.GetComponent<UIButton>();
            if (button != null)
            {
                UnityEngine.Object.Destroy(button);
            }

            inputObject.SetActive(true);
            return inputObject;
        }

        private static GameObject CreateButton(Transform parent, string name, string label, Vector3 position, Vector2 size, Color color)
        {
            Button template = null;

            UIStationWindow stationWindow = UIRoot.instance.uiGame.stationWindow;
            if (stationWindow != null && stationWindow.storageUIPrefab != null)
            {
                template = stationWindow.storageUIPrefab.localSdButton;
            }

            if (template == null)
            {
                template = UnityEngine.Object.FindObjectOfType<Button>();
            }

            if (template == null)
            {
                LogManager.Logger.LogWarning("Stack size editor: no Button template found.");
                return null;
            }

            GameObject buttonObject = UnityEngine.Object.Instantiate(template.gameObject, parent);
            buttonObject.name = name;
            buttonObject.transform.localPosition = position;

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = size;
            }

            Text text = buttonObject.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }

            Image image = buttonObject.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }

            buttonObject.SetActive(true);
            return buttonObject;
        }

        private static void CreateStackSizeButton()
        {
            hotkeyFallback = true;

            UIInventoryWindow inventoryWindow = UIRoot.instance.uiGame.inventoryWindow;
            if (inventoryWindow == null || inventoryWindow.titleText == null)
            {
                LogManager.Logger.LogWarning("Stack size editor: inventory title not found, press F8 to open the editor.");
                return;
            }

            Transform title = inventoryWindow.titleText.transform;
            GameObject template = GameObject.Find("UI Root/Overlay Canvas/In Game/Game Menu/detail-func-group/dfunc-1");
            if (template != null)
            {
                stackSizeButton = UnityEngine.Object.Instantiate(template, title);
            }
            else if (applyButton != null)
            {
                stackSizeButton = UnityEngine.Object.Instantiate(applyButton, title);
            }

            if (stackSizeButton == null)
            {
                LogManager.Logger.LogWarning("Stack size editor: inventory button template not found, press F8 to open the editor.");
                return;
            }

            stackSizeButton.name = "stackSizeButton";
            stackSizeButton.transform.localPosition = new Vector3(114f, -3f, 0f);
            stackSizeButton.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

            RectTransform rect = stackSizeButton.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(27f, 27f);
            }

            UIButton uiButton = stackSizeButton.GetComponent<UIButton>();
            if (uiButton != null)
            {
                uiButton.tips.tipTitle = "Stack size editor".Translate();
                uiButton.tips.tipText = "Click to enter Stack Size edit mode.".Translate();
                uiButton.tips.corner = 4;
                uiButton.tips.offset = new Vector2(0f, 20f);
                uiButton.highlighted = true;
            }

            Transform icon = stackSizeButton.transform.Find("icon");
            if (icon != null)
            {
                RectTransform iconRect = icon.GetComponent<RectTransform>();
                if (iconRect != null)
                {
                    iconRect.sizeDelta = new Vector2(15f, 15f);
                }
                Image image = icon.GetComponent<Image>();
                if (image != null && Main.stackIcon != null)
                {
                    image.sprite = Main.stackIcon;
                }
            }
            else
            {
                Text text = stackSizeButton.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.text = "Stack".Translate();
                }
            }

            Button button = stackSizeButton.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(new UnityAction(OnClickStackSizeButton));
            }

            stackSizeButton.SetActive(true);
            hotkeyFallback = false;
        }

        public static void OnPickerOpened()
        {
            if (Main.stackSizeChangerMode)
            {
                if (numTextsBase != null)
                {
                    numTextsBase.SetActive(true);
                }
                if (resetButton != null)
                {
                    resetButton.SetActive(true);
                }
                if (selectedItem != null)
                {
                    selectedItem.SetActive(true);
                }
                ClearSelectedProto();
                RefreshNumTexts();
            }
            else
            {
                if (selectedItem != null)
                {
                    selectedItem.SetActive(false);
                }
                if (numTextsBase != null)
                {
                    numTextsBase.SetActive(false);
                }
                if (resetButton != null)
                {
                    resetButton.SetActive(false);
                }
            }
        }

        public static void ShowSelectedProto(ItemProto proto)
        {
            if (proto == null)
            {
                return;
            }

            if (selectedItem != null)
            {
                selectedItem.SetActive(true);
            }

            if (selectedItemName != null)
            {
                Text text = selectedItemName.GetComponent<Text>();
                if (text != null)
                {
                    text.text = proto.name;
                }
            }

            if (selectedItemStackSizeInput != null)
            {
                InputField input = selectedItemStackSizeInput.GetComponent<InputField>();
                if (input != null)
                {
                    input.text = proto.StackSize.ToString();
                }
            }
        }

        public static void ClearSelectedProto()
        {
            if (selectedItemName != null)
            {
                Text text = selectedItemName.GetComponent<Text>();
                if (text != null)
                {
                    text.text = "";
                }
            }

            if (selectedItemStackSizeInput != null)
            {
                InputField input = selectedItemStackSizeInput.GetComponent<InputField>();
                if (input != null)
                {
                    input.text = "";
                }
            }
        }

        public static void OnClickStackSizeButton()
        {
            if (UIItemPicker.isOpened)
            {
                Main.stackSizeChangerMode = false;
                UIItemPicker.Close();
            }
            else
            {
                Main.stackSizeChangerMode = true;
                ((ManualBehaviour)UIRoot.instance.uiGame.itemPicker)._Open();
            }
        }

        public static void OnClickApplyButton()
        {
            try
            {
                if (Patch.selectedProto == null || selectedItemStackSizeInput == null)
                {
                    return;
                }

                InputField input = selectedItemStackSizeInput.GetComponent<InputField>();
                if (input == null || !int.TryParse(input.text, out int count) || count <= 0)
                {
                    return;
                }

                ItemProto proto = LDB.items.Select(Patch.selectedProto.ID);
                if (proto == null || proto.StackSize == count)
                {
                    return;
                }

                proto.StackSize = count;
                if (StorageComponent.itemStackCount != null && proto.ID < StorageComponent.itemStackCount.Length)
                {
                    StorageComponent.itemStackCount[proto.ID] = count;
                }

                Main.stackSizeDictionary[proto.ID] = count;
                File.WriteAllText(Main.jsonFilePath, StackSizeJson.ToJson(Main.stackSizeDictionary));

                ClearSelectedProto();
                RefreshNumTexts();
            }
            catch (Exception ex)
            {
                LogManager.Logger.LogError("Apply stack size failed: " + ex);
            }
        }

        public static void OnClickResetButton()
        {
            try
            {
                messageBox = UIMessageBox.Show("DSPMultiplyStackSize".Translate(), "Reset all stack sizes?".Translate(), "Cancel".Translate(), "Reset".Translate(), 0, null, new UIMessageBox.Response(Reset));
            }
            catch (Exception ex)
            {
                LogManager.Logger.LogError("Reset dialog failed: " + ex);
            }
        }

        public static void Reset()
        {
            try
            {
                Main.stackSizeDictionary.Clear();
                File.WriteAllText(Main.jsonFilePath, StackSizeJson.ToJson(Main.stackSizeDictionary));

                foreach (ItemProto proto in LDB.items.dataArray)
                {
                    if (proto == null || proto.ID <= 0)
                    {
                        continue;
                    }

                    if (!Main.stackSizeDictionaryOrigin.TryGetValue(proto.ID, out int origin))
                    {
                        continue;
                    }

                    proto.StackSize = origin;
                    if (StorageComponent.itemStackCount != null && proto.ID < StorageComponent.itemStackCount.Length)
                    {
                        StorageComponent.itemStackCount[proto.ID] = origin;
                    }
                }

                RefreshNumTexts();
            }
            catch (Exception ex)
            {
                LogManager.Logger.LogError("Reset stack sizes failed: " + ex);
            }
        }

        public static void RefreshNumTexts()
        {
            try
            {
                if (numTexts == null || UIRoot.instance == null || UIRoot.instance.uiGame == null)
                {
                    return;
                }

                UIItemPicker picker = UIRoot.instance.uiGame.itemPicker;
                if (picker == null)
                {
                    return;
                }

                ItemProto[] protos = ItemPickerRefs.ProtoArray(picker);
                if (protos == null)
                {
                    return;
                }

                int count = Math.Min(numTexts.Length, protos.Length);
                for (int i = 0; i < count; i++)
                {
                    Text label = numTexts[i];
                    if (label == null)
                    {
                        continue;
                    }

                    ItemProto proto = protos[i];
                    if (proto != null)
                    {
                        label.text = proto.StackSize.ToString();
                        label.gameObject.SetActive(true);
                        label.color = Main.stackSizeDictionary.ContainsKey(proto.ID) ? editedColor : normalColor;
                    }
                    else
                    {
                        label.gameObject.SetActive(false);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Logger.LogWarning("Refresh stack size labels failed: " + ex.Message);
            }
        }
    }
}
