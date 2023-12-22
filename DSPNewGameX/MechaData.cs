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
    internal class MechaData
    {

        public static void Export(BinaryWriter w)
        {
            var mecha = GameMain.mainPlayer.mecha;

            w.Write(6);

            w.Write(mecha.coreEnergyCap);
            w.Write(mecha.coreEnergy);
            w.Write(mecha.corePowerGen);
            w.Write(mecha.reactorPowerGen);
            w.Write(mecha.reactorEnergy);

            w.Write(mecha.reactorItemId);
            w.Write(mecha.reactorItemInc);
            mecha.reactorStorage.Export(w);
            mecha.warpStorage.Export(w);

            w.Write(mecha.walkPower);
            w.Write(mecha.jumpEnergy);
            w.Write(mecha.thrustPowerPerAcc);
            w.Write(mecha.warpKeepingPowerPerSpeed);
            w.Write(mecha.warpStartPowerPerSpeed);
            w.Write(mecha.miningPower);
            w.Write(mecha.replicatePower);
            w.Write(mecha.researchPower);
            w.Write(mecha.droneEjectEnergy);
            w.Write(mecha.droneEnergyPerMeter);
            w.Write(mecha.coreLevel);
            w.Write(mecha.thrusterLevel);
            w.Write(mecha.miningSpeed);
            w.Write(mecha.replicateSpeed);
            w.Write(mecha.walkSpeed);
            w.Write(mecha.jumpSpeed);
            w.Write(mecha.maxSailSpeed);
            w.Write(mecha.maxWarpSpeed);
            w.Write(mecha.buildArea);

            //mecha.forge.Export(w);
            //mecha.lab.Export(w);

            w.Write(mecha.droneCount);
            w.Write(mecha.droneSpeed);
            w.Write(mecha.droneMovement);

            //for (int i = 0; i < mecha.droneCount; i++)
            //{
            //    mecha.drones[i].Export(w);
            //}

            mecha.appearance.Export(w);
            mecha.diyAppearance.Export(w);
            w.Write(mecha.diyItems.items.Count);
            foreach (KeyValuePair<int, int> keyValuePair in mecha.diyItems.items)
            {
                w.Write(keyValuePair.Key);
                w.Write(keyValuePair.Value);
            }
            //w.Write(2119973658);
        }


        public static void Import(BinaryReader r)
        {
            var mecha = GameMain.mainPlayer.mecha;

            int num = r.ReadInt32();
            if (num == 6)
            {
                LogManager.Logger.LogInfo("-------------------------------------mecha.inport start");
            }
            else
            {
                LogManager.Logger.LogInfo("-------------------------------------mecha.inport error:");
            }

            if (UI.UpgradesEnable)//アップグレードを読み込むなら
            {
                mecha.coreEnergyCap = r.ReadDouble();
                mecha.coreEnergy = r.ReadDouble();
                mecha.corePowerGen = r.ReadDouble();
                mecha.reactorPowerGen = r.ReadDouble();
                mecha.reactorEnergy = r.ReadDouble();
            }
            else
            {
                r.ReadDouble();
                r.ReadDouble();
                r.ReadDouble();
                r.ReadDouble();
                r.ReadDouble();
            }
            if (UI.FuelChamberEnable)//燃焼室を読み込むなら
            {
                mecha.reactorItemId = r.ReadInt32();
                mecha.reactorItemInc = r.ReadInt32();
                mecha.reactorStorage.Import(r);
                mecha.warpStorage.Import(r);
            }
            else
            {
                r.ReadInt32();
                r.ReadInt32();
                var reactorStorage = new StorageComponent(4);
                reactorStorage.type = EStorageType.Fuel;
                var warpStorage = new StorageComponent(1);
                warpStorage.type = EStorageType.Filtered;
                reactorStorage.Import(r);
                warpStorage.Import(r);

            }
            if (UI.UpgradesEnable)//アップグレードを読み込むなら
            {
                mecha.walkPower = r.ReadDouble();
                mecha.jumpEnergy = r.ReadDouble();
                mecha.thrustPowerPerAcc = r.ReadDouble();
                mecha.warpKeepingPowerPerSpeed = r.ReadDouble();
                mecha.warpStartPowerPerSpeed = r.ReadDouble();
                mecha.miningPower = r.ReadDouble();
                mecha.replicatePower = r.ReadDouble();
                mecha.researchPower = r.ReadDouble();
                mecha.droneEjectEnergy = r.ReadDouble();
                mecha.droneEnergyPerMeter = r.ReadDouble();
                mecha.coreLevel = r.ReadInt32();
                mecha.thrusterLevel = r.ReadInt32();
                mecha.miningSpeed = r.ReadSingle();
                mecha.replicateSpeed = r.ReadSingle();
                mecha.walkSpeed = r.ReadSingle();
                mecha.jumpSpeed = r.ReadSingle();
                mecha.maxSailSpeed = r.ReadSingle();
                mecha.maxWarpSpeed = r.ReadSingle();
                mecha.buildArea = r.ReadSingle();
                //mecha.forge.Import(r);
                //mecha.lab.Import(r);
                mecha.droneCount = r.ReadInt32();
                mecha.droneSpeed = r.ReadSingle();
                mecha.droneMovement = r.ReadInt32();
            }
            else
            {
                r.ReadDouble();
                r.ReadDouble();
                r.ReadDouble();
                r.ReadDouble();
                r.ReadDouble();
                r.ReadDouble();
                r.ReadDouble();
                r.ReadDouble();
                r.ReadDouble();
                r.ReadDouble();
                r.ReadInt32();
                r.ReadInt32();
                r.ReadSingle();
                r.ReadSingle();
                r.ReadSingle();
                r.ReadSingle();
                r.ReadSingle();
                r.ReadSingle();
                r.ReadSingle();
                r.ReadInt32();
                r.ReadSingle();
                r.ReadInt32();
            }
            //for (int i = 0; i < mecha.droneCount; i++)
            //{
            //    mecha.drones[i].Import(r);
            //}
            //mecha.droneLogic.ReloadStates();
            //mecha.appearance.ResetAppearance();

            mecha.diyAppearance.ResetAppearance();
            mecha.diyItems.Clear();
            if (num < 5)
            {
                if (num >= 1)
                {
                    int num2 = r.ReadInt32();
                    for (int j = 0; j < num2; j++)
                    {
                        if (j < 8)
                        {
                            mecha.appearance.mainColors[j].r = r.ReadByte();
                            mecha.appearance.mainColors[j].g = r.ReadByte();
                            mecha.appearance.mainColors[j].b = r.ReadByte();
                            mecha.appearance.mainColors[j].a = r.ReadByte();
                        }
                        else
                        {
                            r.ReadByte();
                            r.ReadByte();
                            r.ReadByte();
                            r.ReadByte();
                        }
                    }
                    if (num >= 3)
                    {
                        for (int k = 0; k < 64; k++)
                        {
                            for (int l = 0; l < num2; l++)
                            {
                                if (l < 8)
                                {
                                    mecha.appearance.partColors[k, l].r = r.ReadByte();
                                    mecha.appearance.partColors[k, l].g = r.ReadByte();
                                    mecha.appearance.partColors[k, l].b = r.ReadByte();
                                    mecha.appearance.partColors[k, l].a = r.ReadByte();
                                }
                                else
                                {
                                    r.ReadByte();
                                    r.ReadByte();
                                    r.ReadByte();
                                    r.ReadByte();
                                    //}
                                }
                            }
                        }
                    }
                    LogManager.Logger.LogInfo("mecaImport 4 ");
                    if (num >= 2)
                    {
                        mecha.appearance.partHideMask = r.ReadUInt64();
                        if (num >= 3)
                        {
                            mecha.appearance.partCustomMask = r.ReadUInt64();
                        }
                        mecha.appearance.customArmor.Import(r);
                        if (r.ReadInt32() != 2119973658)
                        {
                            throw new Exception("Corrupted Mecha Data");
                        }
                    }
                    mecha.appearance.CopyTo(mecha.diyAppearance);
                    return;
                }
                mecha.appearance.Import(r);
                mecha.diyAppearance.Import(r);
                if (num >= 6)
                {
                    int num3 = r.ReadInt32();
                    for (int m = 0; m < num3; m++)
                    {
                        int key = r.ReadInt32();
                        int value = r.ReadInt32();
                        mecha.diyItems.items[key] = value;
                    }
                }
                //if (r.ReadInt32() != 2119973658)
                //{
                //    throw new Exception("Corrupted Mecha Data");
                //}
            }


        }
    }
}
