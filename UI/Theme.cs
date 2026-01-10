using System.Drawing;
using System.Reflection;

namespace ArdysaModsTools.UI
{
    /// <summary>
    /// Centralized theme constants and assets used across the UI.
    /// </summary>
    public static class Theme
    {
        // --- Assets --------------------------------------------------------
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
            catch
            {
                return new Bitmap(32, 32);
            }
        }

        // --- Sizes ---------------------------------------------------------
        // Larger tile size for better visibility and scaling with window width
        public static int TileSize = 180;
        public static int TileCaptionHeight = 38;
        public static int TileMarginHorizontal = 12;
        public static int TileMarginVertical = 10;

        // Row spacing/padding tuned for compact, clean look
        public static int RowOuterPadding = 10;
        public static int RowPadding = 14;
        public static int RowOuterMargin = 10;

        // --- Colors --------------------------------------------------------
        // Modern black/white minimal theme (matching website)
        public static Color WindowBackground = Color.FromArgb(0, 0, 0);          // #000000
        public static Color RowBackground = Color.FromArgb(5, 5, 5);             // #050505
        public static Color RowBorderColor = Color.FromArgb(51, 51, 51);         // #333333
        public static Color SeparatorColor = Color.FromArgb(51, 51, 51);         // #333333
        public static Color TileBackground = Color.FromArgb(10, 10, 10);         // #0A0A0A
        public static Color TileHover = Color.FromArgb(26, 26, 26);              // #1A1A1A
        public static Color TileHoverBorder = Color.FromArgb(255, 255, 255);     // #FFFFFF
        
        // Accent color - Cyan for cyberpunk style
        public static Color Accent = Color.FromArgb(0, 255, 255);               // Cyan
        public static Color AccentDark = Color.FromArgb(0, 180, 180);           // Dark cyan
        public static Color TileSelected = Color.FromArgb(0, 255, 255);         // Cyan
        public static Color TileSelectedBg = Color.FromArgb(20, 20, 20);        // Dark bg
        
        // Text colors
        public static Color TextLight = Color.FromArgb(255, 255, 255);           // #FFFFFF
        public static Color TextMuted = Color.FromArgb(100, 100, 100);           // #646464
        public static Color TitleColor = Color.FromArgb(255, 255, 255);          // #FFFFFF
        
        // Favorites
        public static Color FavColor = Color.FromArgb(255, 82, 82);              // Red heart
        public static Color FavOffColor = Color.FromArgb(68, 68, 68);            // #444444

        // subtle shadow color for tile visual (used if you later paint)
        public static Color TileShadow = Color.FromArgb(20, 0, 0, 0);

        // --- Fonts ---------------------------------------------------------
        // Uses FontHelper for automatic fallback (JetBrains Mono → Consolas → Segoe UI)
        public static Font TitleFont = FontHelper.Create(14F, FontStyle.Bold, GraphicsUnit.Pixel);
        public static Font TileCaptionFont = FontHelper.Create(13F, FontStyle.Regular, GraphicsUnit.Pixel);
        public static Font SmallFont = FontHelper.Create(12F, FontStyle.Regular, GraphicsUnit.Pixel);
    }
}
