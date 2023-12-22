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
    internal class PlayerData
    {

        public static void Export(BinaryWriter w)
        {
            var player = GameMain.mainPlayer;

            w.Write(2);
            //w.Write(player.planetId);
            //w.Write(player.position.x);
            //w.Write(player.position.y);
            //w.Write(player.position.z);
            //w.Write(player.uPosition.x);
            //w.Write(player.uPosition.y);
            //w.Write(player.uPosition.z);
            //w.Write(player.uRotation.x);
            //w.Write(player.uRotation.y);
            //w.Write(player.uRotation.z);
            //w.Write(player.uRotation.w);
            //w.Write((int)player.movementState);
            //w.Write(player.warpState);
            //w.Write(player.warpCommand);
            //w.Write(player.uVelocity.x);
            //w.Write(player.uVelocity.y);
            //w.Write(player.uVelocity.z);
            //w.Write(player.inhandItemId);
            //w.Write(player.inhandItemCount);
            //w.Write(player.inhandItemInc);
            //player.mecha.Export(w);
            player.package.Export(w);
            //player.navigation.Export(w);
            w.Write(player.sandCount);
            LogManager.Logger.LogInfo("-------------------------------------player.sandCount : " + player.sandCount);

        }


        public static void Import(BinaryReader r)
        {
            var player = GameMain.mainPlayer;

            int num = r.ReadInt32();
            if (num == 2)
            {
                LogManager.Logger.LogInfo("-------------------------------------player.inport start");
            }
            else
            {
                LogManager.Logger.LogInfo("-------------------------------------player.inport error:");
            }
            //r.ReadInt32();
            //Vector3 position;
            //position.x = r.ReadSingle();
            //position.y = r.ReadSingle();
            //position.z = r.ReadSingle();
            //player.position = position;
            //player.uPosition.x = r.ReadDouble();
            //player.uPosition.y = r.ReadDouble();
            //player.uPosition.z = r.ReadDouble();
            //player.uRotation.x = r.ReadSingle();
            //player.uRotation.y = r.ReadSingle();
            //player.uRotation.z = r.ReadSingle();
            //player.uRotation.w = r.ReadSingle();
            //player.movementState = (EMovementState)r.ReadInt32();
            //player.warpState = r.ReadSingle();
            //player.warpCommand = r.ReadBoolean();
            //player.uVelocity.x = r.ReadDouble();
            //player.uVelocity.y = r.ReadDouble();
            //player.uVelocity.z = r.ReadDouble();
            //player.inhandItemId = r.ReadInt32();
            //player.inhandItemCount = r.ReadInt32();
            //if (num >= 2)
            //{
            //	player.inhandItemInc = r.ReadInt32();
            //}
            //else
            //{
            //	player.inhandItemInc = 0;
            //}
            //player.mecha.Import(r);
            if (UI.InventoryEnable)　//インベントリを読み込むなら
            {
                player.package = new StorageComponent(10);
                player.package.type = EStorageType.Default;
                player.package.Import(r);
                player.sandCount = r.ReadInt32();
            }
            else
            {
                var package = new StorageComponent(10);
                package.type = EStorageType.Default;
                package.Import(r);
                r.ReadInt32();

            }
            //if (num >= 1)
            //{
            //	player.navigation.Import(r);
            //}

            //LogManager.Logger.LogInfo("-------------------------------------player.sandCount : " + player.sandCount);

        }
    }
}
