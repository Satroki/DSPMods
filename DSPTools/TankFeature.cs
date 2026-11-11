using System.Runtime.CompilerServices;
using HarmonyLib;

namespace DSPTools
{
    /// <summary>
    /// 储罐功能：整段替换 TankComponent.GameTick，实现储罐之间的链式输送，
    /// 并在输送堵死时吞掉特殊流体，避免皮带堵住。
    ///
    /// 由 LoadConfig.EnableTank 控制是否注册；关闭时不打补丁，完全走原版逻辑。
    /// 该功能不依赖本模组的其它模块，可以单独搬到独立项目里。
    /// </summary>
    [HarmonyPatch(typeof(TankComponent), "GameTick")]
    internal static class TankGameTickPatch
    {
        internal const string HarmonyId = "Satroki.DSP.plugin.DSPTools.tank";

        [HarmonyPrefix]
        private static bool Prefix(ref TankComponent __instance, PlanetFactory factory)
        {
            if (__instance.fluidInc < 0)
            {
                __instance.fluidInc = 0;
            }

            if (!__instance.isBottom)
            {
                return false;
            }

            CargoTraffic cargoTraffic = factory.cargoTraffic;
            TankComponent[] tankPool = factory.factoryStorage.tankPool;
            int tmpTankId = __instance.inputSwitch ? __instance.id : 0;

            if (__instance.belt0 > 0)
            {
                TankComponentUpdateBelt(ref __instance, cargoTraffic, tankPool, ref tmpTankId, __instance.belt0, __instance.isOutput0);
            }

            if (__instance.belt1 > 0)
            {
                TankComponentUpdateBelt(ref __instance, cargoTraffic, tankPool, ref tmpTankId, __instance.belt1, __instance.isOutput1);
            }

            if (__instance.belt2 > 0)
            {
                TankComponentUpdateBelt(ref __instance, cargoTraffic, tankPool, ref tmpTankId, __instance.belt2, __instance.isOutput2);
            }

            if (__instance.belt3 > 0)
            {
                TankComponentUpdateBelt(ref __instance, cargoTraffic, tankPool, ref tmpTankId, __instance.belt3, __instance.isOutput3);
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TankComponentUpdateBelt(ref TankComponent thisTank, CargoTraffic cargoTraffic, TankComponent[] tankPool, ref int tmpTankId, int belt, bool isOutput)
        {
            if (isOutput && thisTank.outputSwitch)
            {
                if (thisTank.fluidId > 0 && thisTank.fluidCount > 0)
                {
                    int num = ((thisTank.fluidInc != 0) ? (thisTank.fluidInc / thisTank.fluidCount) : 0);
                    if (cargoTraffic.TryInsertItemAtHead(belt, thisTank.fluidId, 1, (byte)num))
                    {
                        thisTank.fluidCount--;
                        thisTank.fluidInc -= num;
                    }
                }
            }
            else if (!isOutput && thisTank.inputSwitch)
            {
                byte stack;
                byte inc;
                if (thisTank.fluidId > 0)
                {
                    if (thisTank.fluidCount < thisTank.fluidCapacity && cargoTraffic.TryPickItemAtRear(belt, thisTank.fluidId, null, out stack, out inc) > 0)
                    {
                        thisTank.fluidCount += stack;
                        thisTank.fluidInc += inc;
                    }
                }

                if (thisTank.fluidId == 0)
                {
                    int num2 = cargoTraffic.TryPickItemAtRear(belt, 0, ItemProto.fluids, out stack, out inc);
                    if (num2 > 0)
                    {
                        thisTank.fluidId = num2;
                        thisTank.fluidCount += stack;
                        thisTank.fluidInc += inc;
                    }
                }

                if (thisTank.fluidCount >= thisTank.fluidCapacity && cargoTraffic.GetItemIdAtRear(belt) == thisTank.fluidId)
                {
                    // mod 功能：链式输送一点都送不进去时，特殊流体直接吞掉，避免皮带堵死
                    bool moved = TryPickItem(ref thisTank, tankPool, cargoTraffic, ref tmpTankId, belt);
                    if (!moved && IsSpecialFluid(thisTank.fluidId))
                    {
                        cargoTraffic.TryPickItemAtRear(belt, thisTank.fluidId, null, out _, out _);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryPickItem(ref TankComponent thisTank, TankComponent[] tankPool, CargoTraffic cargoTraffic, ref int tmpTankId, int belt)
        {
            bool moved = false;
            int num = 20;
            while (tmpTankId > 0 && --num > 0)
            {
                ref TankComponent tank = ref tankPool[tmpTankId];
                if ((tank.fluidId != 0 && tank.fluidId != thisTank.fluidId) || !tank.inputSwitch)
                {
                    tmpTankId = 0;
                    break;
                }

                byte stack;
                byte inc;
                if (tank.fluidCount >= tank.fluidCapacity)
                {
                    tmpTankId = tank.nextTankId;
                }
                else if (cargoTraffic.TryPickItemAtRear(belt, thisTank.fluidId, null, out stack, out inc) > 0)
                {
                    if (tank.fluidCount == 0)
                    {
                        tank.fluidId = thisTank.fluidId;
                    }

                    tank.fluidCount += stack;
                    tank.fluidInc += inc;
                    moved = true;
                }
            }

            return moved;
        }

        private static bool IsSpecialFluid(int id)
        {
            return id == 1120 || id == 1114;
        }
    }
}
