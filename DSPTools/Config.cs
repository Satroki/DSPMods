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
        public static ConfigEntry<bool> EnableDarkFogDysonDebuffCap;
        public static ConfigEntry<float> DarkFogDysonDebuffCapPercent;
        public static ConfigEntry<bool> EnableDFSignalTowerPriority;
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

            EnableDarkFogDysonDebuffCap = config.Bind(Section, nameof(EnableDarkFogDysonDebuffCap), defaultValue: true,
                "黑雾截取戴森球发电量上限：把黑雾太空巢穴的伽马接收器造成的发电量损失限制在指定比例内（原版没有上限，关闭后恢复原版逻辑）");
            DarkFogDysonDebuffCapPercent = config.Bind(Section, nameof(DarkFogDysonDebuffCapPercent), defaultValue: 100f,
                new ConfigDescription("黑雾最多可截取的戴森球发电量百分比（0-100）。0 = 完全无法截取；100 = 最多截光，但不会出现负数发电。原版每个伽马接收器让蜂群部分损失 0.03%、壳体部分损失 0.07%，数量足够多时会超过 100%",
                    new AcceptableValueRange<float>(0f, 100f)));

            EnableDFSignalTowerPriority = config.Bind(Section, nameof(EnableDFSignalTowerPriority), defaultValue: true,
                "信号塔优先：地面黑雾发动进攻波时，只要星球上有正常供电（供电率≥10%）的信号塔，就只把最近的信号塔作为目标，电网节点不再被选为进攻方向");
        }
    }
}
