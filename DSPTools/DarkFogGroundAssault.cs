using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;

namespace DSPTools
{
    /// <summary>
    /// 地面黑雾波次目标：把信号塔的固定权重从 10 提高到 1e9。
    ///
    /// 原版：num32 = 10f * (200000f / (d² + 120000f))，然后与电网节点最高分 num20 比较。
    /// 电网节点的分数再高也远小于 1e9 量级，所以只要存在正常供电（供电率≥10%）的信号塔，
    /// 最近的信号塔一定胜出，电网节点不再可能被选中。
    ///
    /// 只改这一个浮点常量，选点、50m 过冲和 LaunchAssault 调用都保持原版。
    /// 由 LoadConfig.EnableDFSignalTowerPriority 控制是否注册。
    /// </summary>
    [HarmonyPatch(typeof(DFGBaseComponent), nameof(DFGBaseComponent.UpdateFactoryThreat))]
    internal static class DFGSignalTowerPriorityPatch
    {
        internal const string HarmonyId = "Satroki.DSP.plugin.DSPTools.dfgtower";

        // 必须是有限值：如果用 Infinity，所有信号塔分数并列，先遍历到的（而不是最近的）会胜出。
        private const float BeaconWeight = 1e9f;

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> list = instructions.ToList();
            bool patched = false;

            for (int i = 0; i < list.Count; i++)
            {
                CodeInstruction code = list[i];
                if (code.opcode != OpCodes.Ldc_R4 || code.operand is not float weight || weight != 10f)
                {
                    continue;
                }

                if (!IsBeaconWeightPattern(list, i))
                {
                    continue;
                }

                code.operand = BeaconWeight;
                patched = true;
                break;
            }

            if (!patched)
            {
                throw new InvalidOperationException(
                    "DFGSignalTowerPriorityPatch: 在 DFGBaseComponent.UpdateFactoryThreat 中没有找到信号塔权重 10f，补丁未生效");
            }

            return list;
        }

        /// <summary>
        /// 确认这个 10f 是信号塔权重，而不是方法里其它恰好等于 10 的常量：
        /// ldc.r4 10; ldloc; mul; conv.r8; stloc; ldloc; ldloc; ble.un
        /// </summary>
        private static bool IsBeaconWeightPattern(List<CodeInstruction> list, int index)
        {
            if (index + 7 >= list.Count)
            {
                return false;
            }

            return IsLocalLoad(list[index + 1])
                && list[index + 2].opcode == OpCodes.Mul
                && list[index + 3].opcode == OpCodes.Conv_R8
                && IsLocalStore(list[index + 4])
                && IsLocalLoad(list[index + 5])
                && IsLocalLoad(list[index + 6])
                && (list[index + 7].opcode == OpCodes.Ble_Un || list[index + 7].opcode == OpCodes.Ble_Un_S);
        }

        private static bool IsLocalLoad(CodeInstruction code)
        {
            return code.opcode == OpCodes.Ldloc || code.opcode == OpCodes.Ldloc_S ||
                   code.opcode == OpCodes.Ldloc_0 || code.opcode == OpCodes.Ldloc_1 ||
                   code.opcode == OpCodes.Ldloc_2 || code.opcode == OpCodes.Ldloc_3;
        }

        private static bool IsLocalStore(CodeInstruction code)
        {
            return code.opcode == OpCodes.Stloc || code.opcode == OpCodes.Stloc_S ||
                   code.opcode == OpCodes.Stloc_0 || code.opcode == OpCodes.Stloc_1 ||
                   code.opcode == OpCodes.Stloc_2 || code.opcode == OpCodes.Stloc_3;
        }
    }
}
