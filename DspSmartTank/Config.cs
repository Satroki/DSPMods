using BepInEx.Configuration;
namespace SmartTankS
{
    public static class LoadConfig
    {
        public static ConfigEntry<bool> InfiniteSank;
        public static ConfigEntry<bool> FreeStuff;
        const string Section = "General";
        internal static void Init(ConfigFile config)
        {
            InfiniteSank = config.Bind(Section, nameof(InfiniteSank), defaultValue: false, "无限沙土");
            FreeStuff = config.Bind(Section, nameof(FreeStuff), defaultValue: true, "分流器无限资源");
        }
    }
}
