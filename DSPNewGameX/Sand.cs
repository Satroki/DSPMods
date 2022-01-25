using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSPNewGameX
{
    [HarmonyPatch(typeof(Player), "SetSandCount")]
    public class SandPatch
    {
        private static void Prefix(ref int newSandCount)
        {
            newSandCount = 10000000;
        }
    }
}
