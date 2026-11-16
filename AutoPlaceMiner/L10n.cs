namespace AutoPlaceMiner
{
    /// <summary>
    /// 游戏内提示只区分中文/英文：中文客户端显示中文，其它语言显示英文。
    /// </summary>
    internal static class L10n
    {
        public static bool IsChinese
        {
            get
            {
                try
                {
                    int index = Localization.CurrentLanguageIndex;
                    var languages = Localization.Languages;
                    if (languages == null || index < 0 || index >= languages.Length) return false;
                    int lcId = languages[index].lcId;
                    return lcId == Localization.LCID_ZHCN || lcId == 1028;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static string T(string chinese, string english)
        {
            return IsChinese ? chinese : english;
        }
    }
}
