using BepInEx;
using HarmonyLib;
using System.Runtime.CompilerServices;

namespace SmartTankS
{
    [BepInPlugin("Satroki.DSP.plugin.SmartTankS", "SmartTankS", "1.0.0")]
    [BepInProcess("DSPGAME.exe")]
    public class SmartTankS : BaseUnityPlugin
    {
        public void Start()
        {
            LoadConfig.Init(Config);

            Harmony.CreateAndPatchAll(typeof(SmartTankS));
            if (LoadConfig.InfiniteSank.Value)
                Harmony.CreateAndPatchAll(typeof(SandPatch));
            if (LoadConfig.FreeStuff.Value)
                Harmony.CreateAndPatchAll(typeof(CargoTrafficPatch));
            if (LoadConfig.FreeBuyTech.Value)
                Harmony.CreateAndPatchAll(typeof(GameHistoryDataPatch));
        }


        static bool IsSpecialFluid(int id)
        {
            return id == 1120 || id == 1114;
        }

        [HarmonyPrefix()]
        [HarmonyPatch(typeof(TankComponent), "GameTick")]
        public static bool TankGameTickPatch(ref TankComponent __instance, PlanetFactory factory)
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
            var tankPool = factory.factoryStorage.tankPool;

            var belt = __instance.belt0;
            if (belt > 0)
            {
                TankComponentUpdateBelt(ref __instance, belt, __instance.isOutput0, ref cargoTraffic, ref tankPool);
            }

            belt = __instance.belt1;
            if (belt > 0)
            {
                TankComponentUpdateBelt(ref __instance, belt, __instance.isOutput1, ref cargoTraffic, ref tankPool);
            }

            belt = __instance.belt2;
            if (belt > 0)
            {
                TankComponentUpdateBelt(ref __instance, belt, __instance.isOutput2, ref cargoTraffic, ref tankPool);
            }

            belt = __instance.belt3;
            if (belt > 0)
            {
                TankComponentUpdateBelt(ref __instance, belt, __instance.isOutput3, ref cargoTraffic, ref tankPool);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TankComponentUpdateBelt(ref TankComponent thisTank, int belt, bool isOutput, ref CargoTraffic cargoTraffic, ref TankComponent[] tankPool)
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
                    else if (thisTank.fluidCount >= thisTank.fluidCapacity && IsSpecialFluid(thisTank.fluidId) && thisTank.nextTankId <= 0)
                    {
                        cargoTraffic.TryPickItemAtRear(belt, thisTank.fluidId, null, out _, out _);
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

                if (thisTank.fluidCount >= thisTank.fluidCapacity && cargoTraffic.GetItemIdAtRear(belt) == thisTank.fluidId && thisTank.nextTankId > 0)
                {
                    TankComponent tankComponent = tankPool[thisTank.nextTankId];
                    TankComponent tankComponent2 = tankComponent;
                    while (tankComponent.fluidCount >= tankComponent.fluidCapacity)
                    {
                        TankComponent tankComponent3 = tankPool[tankComponent2.lastTankId];
                        if (tankComponent.fluidId != tankComponent3.fluidId)
                        {
                            tankComponent2 = tankComponent3;
                            break;
                        }

                        if (tankComponent.inputSwitch)
                        {
                            if (tankComponent.nextTankId > 0)
                            {
                                tankComponent = tankPool[tankComponent.nextTankId];
                                tankComponent2 = tankComponent;
                                continue;
                            }

                            tankComponent2.id = thisTank.id;
                            break;
                        }

                        tankComponent2 = tankPool[tankComponent2.lastTankId];
                        break;
                    }

                    TankComponent tankComponent4 = tankPool[tankComponent2.lastTankId];
                    if (!tankComponent2.inputSwitch || (tankComponent2.fluidId != tankComponent4.fluidId && tankComponent2.fluidId != 0))
                    {
                        tankComponent2 = tankComponent4;
                    }

                    bool flag = true;
                    if (((tankComponent2.id == thisTank.id || tankComponent2.fluidCount >= tankComponent2.fluidCapacity) && !IsSpecialFluid(thisTank.fluidId)) || !tankComponent4.outputSwitch)
                    {
                        flag = false;
                    }

                    if (flag && cargoTraffic.TryPickItemAtRear(belt, thisTank.fluidId, null, out stack, out inc) > 0)
                    {
                        if (tankPool[tankComponent2.id].fluidCount == 0)
                        {
                            tankPool[tankComponent2.id].fluidId = thisTank.fluidId;
                        }
                        if (tankPool[tankComponent2.id].fluidCount < tankPool[tankComponent2.id].fluidCapacity)
                        {
                            tankPool[tankComponent2.id].fluidCount += stack;
                            tankPool[tankComponent2.id].fluidInc += inc;
                        }
                    }
                }
            }
        }
    }
}
