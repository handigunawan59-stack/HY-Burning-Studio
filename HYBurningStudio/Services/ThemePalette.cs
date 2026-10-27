using System.Windows;
using System.Windows.Media;

namespace HYBurningStudio.Services;

public static class ThemePalette
{
    public static void Apply(string theme)
    {
        bool dark = theme == "dark";
        var resources = Application.Current.Resources;
        void Color(string key, string light, string night) => resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? night : light));
        Color("ThemeBackground", "#F4F6FA", "#101925");
        Color("ThemeSidebar", "#EDF1F7", "#172230");
        Color("ThemeText", "#17243D", "#EAF1FB");
        Color("ThemeHint", "#75859C", "#B3C0D1");
        Color("ThemeCard", "#FFFFFF", "#202E3E");
        Color("ThemeBorder", "#E6EBF2", "#3A4B5E");
        Color("ThemeInput", "#F7F9FC", "#293A4C");
        Color("ThemeButtonText", "#40516C", "#E8F0FB");
        Color("ThemeSeparator", "#DCE3ED", "#3A4B5E");
        Color("ThemeNavSelected", "#DDE8FF", "#284B79");
        Color("ThemeNavSelectedText", "#1E5FE2", "#D5E7FF");
        Color("ThemeNavText", "#64748B", "#B9C8DA");
        Color("ThemeAccentText", "#286BEF", "#93BCFF");
        Color("ThemePurple", "#8B5CD6", "#C8AAFF");
        Color("ThemeGreen", "#278C78", "#80D6BD");
    }
}
