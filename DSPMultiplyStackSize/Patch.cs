using System;
using HarmonyLib;

namespace DSPMultiplyStackSize
{
    [HarmonyPatch]
    internal class Patch
    {
        public static ItemProto selectedProto;

        [HarmonyPatch(typeof(UIMechaWindow), "_OnOpen")]
        public static class UIMechaWindow_OnOpen
        {
            [HarmonyPostfix]
            public static void Postfix(UIMechaWindow __instance)
            {
                if (!Main.editWarperSlotStackSize.Value)
                {
                    return;
                }

                try
                {
                    Player player = GameMain.mainPlayer;
                    if (player == null || player.mecha == null)
                    {
                        return;
                    }

                    StorageComponent storage = player.mecha.warpStorage;
                    if (storage == null || storage.grids == null || storage.grids.Length == 0)
                    {
                        return;
                    }

                    int[] stackCounts = StorageComponent.itemStackCount;
                    if (stackCounts == null || 1210 >= stackCounts.Length)
                    {
                        return;
                    }

                    storage.grids[0].filter = 1210;
                    storage.grids[0].itemId = 1210;
                    storage.grids[0].stackSize = stackCounts[1210];
                }
                catch (Exception ex)
                {
                    LogManager.Logger.LogWarning("UIMechaWindow._OnOpen patch failed: " + ex);
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIItemPicker), "OnBoxMouseDown")]
        public static bool UIItemPicker_OnBoxMouseDowne_PrePatch(UIItemPicker __instance)
        {
            if (!Main.stackSizeChangerMode)
            {
                return true;
            }

            try
            {
                ItemProto[] protos = ItemPickerRefs.ProtoArray(__instance);
                int index = ItemPickerRefs.HoveredIndex(__instance);
                if (protos != null && index >= 0 && index < protos.Length)
                {
                    ItemProto proto = protos[index];
                    if (proto != null)
                    {
                        selectedProto = proto;
                        UI.ShowSelectedProto(proto);
                        return false;
                    }
                }

                UI.ClearSelectedProto();
            }
            catch (Exception ex)
            {
                LogManager.Logger.LogWarning("UIItemPicker.OnBoxMouseDown patch failed: " + ex);
            }

            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIItemPicker), "OnTypeButtonClick")]
        public static void UIItemPicker_OnTypeButtonClick_PostPatch(UIItemPicker __instance)
        {
            if (!Main.stackSizeChangerMode)
            {
                return;
            }

            UI.ClearSelectedProto();
            UI.RefreshNumTexts();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIItemPicker), "_OnOpen")]
        public static void UIItemPicker_OnUpdate_Patch(UIItemPicker __instance)
        {
            UI.OnPickerOpened();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIItemPicker), "_OnClose")]
        public static void UIItemPicker_Close_Patch(UIItemPicker __instance)
        {
            Main.stackSizeChangerMode = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(VFPreload), "InvokeOnLoadWorkEnded")]
        [HarmonyPriority(1)]
        public static void VFPreload_PreloadThread_Patch()
        {
            if (Main.StackSizeUpdated)
            {
                return;
            }

            try
            {
                LogManager.Logger.LogInfo("Apply Multiply Stack Size.");

                ItemProto[] items = LDB.items.dataArray;
                int[] stackCounts = StorageComponent.itemStackCount;
                int multiplier = Main.configMultiplyStackSize.Value;

                foreach (ItemProto proto in items)
                {
                    if (proto == null || proto.ID <= 0)
                    {
                        continue;
                    }

                    int count = proto.StackSize * multiplier;
                    Main.stackSizeDictionaryOrigin[proto.ID] = count;

                    if (Main.stackSizeDictionary.TryGetValue(proto.ID, out int custom))
                    {
                        count = custom;
                    }

                    proto.StackSize = count;
                    if (stackCounts != null && proto.ID < stackCounts.Length)
                    {
                        stackCounts[proto.ID] = count;
                    }
                }

                Main.StackSizeUpdated = true;

                ItemProto sample = LDB.items.Select(1001);
                if (sample != null)
                {
                    LogManager.Logger.LogInfo("Stack size check: item 1001 = " + sample.StackSize);
                }
            }
            catch (Exception ex)
            {
                LogManager.Logger.LogError("Apply Multiply Stack Size failed: " + ex);
            }
        }
    }

    /// <summary>
    /// UIItemPicker.protoArray / hoveredIndex became private, so access them through Harmony refs.
    /// </summary>
    internal static class ItemPickerRefs
    {
        public static readonly AccessTools.FieldRef<UIItemPicker, ItemProto[]> ProtoArray =
            AccessTools.FieldRefAccess<UIItemPicker, ItemProto[]>("protoArray");

        public static readonly AccessTools.FieldRef<UIItemPicker, int> HoveredIndex =
            AccessTools.FieldRefAccess<UIItemPicker, int>("hoveredIndex");
    }
}
