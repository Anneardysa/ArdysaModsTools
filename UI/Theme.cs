using System.Drawing;
using System.Reflection;

namespace ArdysaModsTools.UI
{
    public static class Theme
    {
        public static bool IsDarkMode { get; private set; } = true;
        public static event Action? ThemeChanged;

        public static void SetTheme(bool darkMode)
        {
            IsDarkMode = darkMode;
            ThemeChanged?.Invoke();
        }

        // Assets
        public static Image FavIconOn { get; } = LoadImage("ArdysaModsTools.Assets.Icons.fav_on.png");
        public static Image FavIconOff { get; } = LoadImage("ArdysaModsTools.Assets.Icons.fav_off.png");

        private static Image LoadImage(string resourcePath)
        {
            var asm = Assembly.GetExecutingAssembly();
            try
            {
                using var stream = asm.GetManifestResourceStream(resourcePath);
                return stream != null ? Image.FromStream(stream) : new Bitmap(32, 32);
            }
            catch { return new Bitmap(32, 32); }
        }

        // Sizes
        public static int TileSize = 180;
        public static int TileCaptionHeight = 38;
        public static int TileMarginHorizontal = 12;
        public static int TileMarginVertical = 10;
        public static int RowOuterPadding = 10;
        public static int RowPadding = 14;
        public static int RowOuterMargin = 10;

        // Dynamic Colors
        public static Color WindowBackground => IsDarkMode ? Color.FromArgb(0, 0, 0) : Color.FromArgb(245, 245, 245);
        public static Color RowBackground => IsDarkMode ? Color.FromArgb(5, 5, 5) : Color.FromArgb(250, 250, 250);
        public static Color RowBorderColor => IsDarkMode ? Color.FromArgb(51, 51, 51) : Color.FromArgb(200, 200, 200);
        public static Color SeparatorColor => IsDarkMode ? Color.FromArgb(51, 51, 51) : Color.FromArgb(200, 200, 200);
        public static Color TileBackground => IsDarkMode ? Color.FromArgb(10, 10, 10) : Color.FromArgb(240, 240, 240);
        public static Color TileHover => IsDarkMode ? Color.FromArgb(26, 26, 26) : Color.FromArgb(220, 220, 220);
        public static Color TileHoverBorder => IsDarkMode ? Color.FromArgb(255, 255, 255) : Color.FromArgb(100, 100, 100);

        // Accent colors
        public static Color Accent => Color.FromArgb(0, 255, 255);
        public static Color AccentDark => Color.FromArgb(0, 180, 180);
        public static Color TileSelected => Color.FromArgb(0, 255, 255);
        public static Color TileSelectedBg => IsDarkMode ? Color.FromArgb(20, 20, 20) : Color.FromArgb(230, 250, 250);

        // Text colors
        public static Color TextLight => IsDarkMode ? Color.FromArgb(255, 255, 255) : Color.FromArgb(30, 30, 30);
        public static Color TextMuted => IsDarkMode ? Color.FromArgb(100, 100, 100) : Color.FromArgb(120, 120, 120);
        public static Color TitleColor => IsDarkMode ? Color.FromArgb(255, 255, 255) : Color.FromArgb(30, 30, 30);

        public static Color FavColor => Color.FromArgb(255, 82, 82);
        public static Color FavOffColor => IsDarkMode ? Color.FromArgb(68, 68, 68) : Color.FromArgb(180, 180, 180);
        public static Color TileShadow => Color.FromArgb(20, 0, 0, 0);

        // Fonts
        public static Font TitleFont = FontHelper.Create(14F, FontStyle.Bold, GraphicsUnit.Pixel);
        public static Font TileCaptionFont = FontHelper.Create(13F, FontStyle.Regular, GraphicsUnit.Pixel);
        public static Font SmallFont = FontHelper.Create(12F, FontStyle.Regular, GraphicsUnit.Pixel);
    }
}
