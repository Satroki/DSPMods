using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSPNewGameX
{
    public static class LoadConfig
    {
        public static ConfigEntry<bool> LoadReactor;
        public static ConfigEntry<bool> LoadPackage;
        public static ConfigEntry<bool> LoadTech;
        public static ConfigEntry<bool> NotClear;

        const string Section = "General";
        internal static void Init(ConfigFile config)
        {
            LoadReactor = config.Bind(Section, nameof(LoadReactor), defaultValue: true, "导入燃烧室物品");
            LoadPackage = config.Bind(Section, nameof(LoadPackage), defaultValue: true, "导入背包物品");
            LoadTech = config.Bind(Section, nameof(LoadTech), defaultValue: true, "导入科技");
            NotClear = config.Bind(Section, nameof(NotClear), defaultValue: false, "不解锁通关");
            //LoadUpgrade = config.Bind("config", nameof(LoadUpgrade), defaultValue: true, "导入升级");
        }
    }
}
