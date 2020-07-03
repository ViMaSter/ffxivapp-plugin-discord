namespace FFXIVAPP.Plugin.Discord.Helpers {
    using System.Drawing;
    using System.Windows;
    using System.Windows.Media;

    using FFXIVAPP.Common.Controls;
    using FFXIVAPP.Plugin.Discord.Properties;

    using FontFamily = System.Windows.Media.FontFamily;

    internal static class ThemeHelper {
        public static void SetupColor(ref xFlowDocument flowDoc) {
            flowDoc._FD.Background = new SolidColorBrush(Settings.Default.ChatBackgroundColor);
        }

        public static void SetupFont(ref xFlowDocument flowDoc) {
            Font font = Settings.Default.ChatFont;
            flowDoc._FD.FontFamily = new FontFamily(font.Name);
            flowDoc._FD.FontWeight = font.Bold
                                         ? FontWeights.Bold
                                         : FontWeights.Regular;
            flowDoc._FD.FontStyle = font.Italic
                                        ? FontStyles.Italic
                                        : FontStyles.Normal;
            flowDoc._FD.FontSize = font.Size;
        }
    }
}