using BepInEx.Configuration;
namespace DSPTools
{
    public static class LoadConfig
    {
        public static ConfigEntry<bool> EnableTank;
        public static ConfigEntry<bool> EnableAchievements;
        public static ConfigEntry<bool> InfiniteSank;
        public static ConfigEntry<bool> FreeStuff;
        public static ConfigEntry<bool> FreeBuyTech;
        public static ConfigEntry<bool> EnableCustomResolution;
        public static ConfigEntry<string> CustomResolutions;
        const string Section = "General";
        const string ResolutionSection = "Resolution";
        internal static void Init(ConfigFile config)
        {
            EnableTank = config.Bind(Section, nameof(EnableTank), defaultValue: true,
                "储罐功能：储罐之间链式输送，堵死时吞掉特殊流体（整段替换 TankComponent.GameTick，关闭后恢复原版逻辑）");
            EnableAchievements = config.Bind(Section, nameof(EnableAchievements), defaultValue: true,
                "装 mod 也允许解锁成就（屏蔽游戏的异常检测，并清空存档里的异常记录）");
            InfiniteSank = config.Bind(Section, nameof(InfiniteSank), defaultValue: false, "无限沙土");
            FreeStuff = config.Bind(Section, nameof(FreeStuff), defaultValue: false, "分流器无限资源");
            FreeBuyTech = config.Bind(Section, nameof(FreeBuyTech), defaultValue: true, "免费买科技");

            EnableCustomResolution = config.Bind(ResolutionSection, nameof(EnableCustomResolution), defaultValue: true,
                "把自定义分辨率注入游戏的“视频分辨率”下拉框（修改后需重启游戏）");
            CustomResolutions = config.Bind(ResolutionSection, nameof(CustomResolutions), defaultValue: "2160x1300",
                "自定义分辨率列表，逗号/分号/换行分隔，格式：宽x高[@刷新率]，例如：2560x1440@165, 1920x1080。省略刷新率时使用当前显示器的刷新率。");
        }
    }
}
