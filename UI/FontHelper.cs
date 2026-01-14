/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ArdysaModsTools.UI
{
    /// <summary>
    /// Helper class for font management with embedded font support.
    /// Loads JetBrains Mono from embedded files, falls back to installed fonts.
    /// </summary>
    public static class FontHelper
    {
        private static readonly string[] PreferredFonts = { "JetBrains Mono", "Consolas", "Cascadia Code", "Segoe UI" };
        private static readonly string[] MonoFonts = { "JetBrains Mono", "Consolas", "Cascadia Code", "Courier New" };

        private static string? _cachedFontFamily;
        private static string? _cachedMonoFamily;
        private static PrivateFontCollection? _privateFonts;
        private static bool _fontsLoaded = false;

        /// <summary>
        /// Static constructor - loads embedded fonts on first use.
        /// </summary>
        static FontHelper()
        {
            LoadEmbeddedFonts();
        }

        /// <summary>
        /// Gets the best available font family from the preferred list.
        /// </summary>
        public static string FontFamily
        {
            get
            {
                if (_cachedFontFamily != null)
                    return _cachedFontFamily;

                _cachedFontFamily = GetAvailableFont(PreferredFonts) ?? "Microsoft Sans Serif";
                return _cachedFontFamily;
            }
        }

        /// <summary>
        /// Gets the best available monospace font family.
        /// </summary>
        public static string MonoFontFamily
        {
            get
            {
                if (_cachedMonoFamily != null)
                    return _cachedMonoFamily;

                _cachedMonoFamily = GetAvailableFont(MonoFonts) ?? "Courier New";
                return _cachedMonoFamily;
            }
        }

        /// <summary>
        /// Gets the private font collection with embedded fonts.
        /// </summary>
        public static PrivateFontCollection? PrivateFonts => _privateFonts;

        /// <summary>
        /// Load embedded fonts from Assets/Fonts folder.
        /// </summary>
        private static void LoadEmbeddedFonts()
        {
            if (_fontsLoaded) return;
            _fontsLoaded = true;

            try
            {
                var fontsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Fonts");
                if (!Directory.Exists(fontsFolder))
                {
                    System.Diagnostics.Debug.WriteLine($"FontHelper: Fonts folder not found: {fontsFolder}");
                    return;
                }

                var fontFiles = Directory.GetFiles(fontsFolder, "*.ttf")
                    .Concat(Directory.GetFiles(fontsFolder, "*.otf"))
                    .ToArray();

                if (fontFiles.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine("FontHelper: No font files found in Assets/Fonts");
                    return;
                }

                _privateFonts = new PrivateFontCollection();

                foreach (var fontFile in fontFiles)
                {
                    try
                    {
                        _privateFonts.AddFontFile(fontFile);
                        System.Diagnostics.Debug.WriteLine($"FontHelper: Loaded font: {Path.GetFileName(fontFile)}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"FontHelper: Failed to load {fontFile}: {ex.Message}");
                    }
                }

                // Log loaded font families
                foreach (var family in _privateFonts.Families)
                {
                    System.Diagnostics.Debug.WriteLine($"FontHelper: Available private font: {family.Name}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FontHelper: Error loading fonts: {ex.Message}");
            }
        }

        /// <summary>
        /// Create a font with automatic fallback, preferring embedded fonts.
        /// </summary>
        public static Font Create(float size, FontStyle style = FontStyle.Regular)
        {
            return CreateFont(FontFamily, size, style);
        }

        /// <summary>
        /// Create a monospace font with automatic fallback.
        /// </summary>
        public static Font CreateMono(float size, FontStyle style = FontStyle.Regular)
        {
            return CreateFont(MonoFontFamily, size, style);
        }

        /// <summary>
        /// Create a font with specific unit.
        /// </summary>
        public static Font Create(float size, FontStyle style, GraphicsUnit unit)
        {
            return CreateFont(FontFamily, size, style, unit);
        }

        /// <summary>
        /// Create a font, trying private fonts first, then installed fonts.
        /// </summary>
        private static Font CreateFont(string familyName, float size, FontStyle style, GraphicsUnit unit = GraphicsUnit.Point)
        {
            // Try private (embedded) fonts first
            if (_privateFonts != null)
            {
                var privateFamily = _privateFonts.Families
                    .FirstOrDefault(f => f.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase));

                if (privateFamily != null)
                {
                    try
                    {
                        return new Font(privateFamily, size, style, unit);
                    }
                    catch
                    {
                        // Fall through to installed fonts
                    }
                }
            }

            // Fall back to installed fonts
            return new Font(familyName, size, style, unit);
        }

        /// <summary>
        /// Find the first available font from the list of candidates.
        /// Checks both private and installed fonts.
        /// </summary>
        private static string? GetAvailableFont(string[] candidates)
        {
            // Check private fonts first
            if (_privateFonts != null)
            {
                var privateNames = _privateFonts.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var font in candidates)
                {
                    if (privateNames.Contains(font))
                        return font;
                }
            }

            // Then check installed fonts
            using var installedFonts = new InstalledFontCollection();
            var installedNames = installedFonts.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var font in candidates)
            {
                if (installedNames.Contains(font))
                    return font;
            }

            return null;
        }

        /// <summary>
        /// Check if a specific font is available (installed or embedded).
        /// </summary>
        public static bool IsFontAvailable(string fontName)
        {
            // Check private fonts
            if (_privateFonts != null)
            {
                if (_privateFonts.Families.Any(f => f.Name.Equals(fontName, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            // Check installed fonts
            using var installedFonts = new InstalledFontCollection();
            return installedFonts.Families.Any(f =>
                f.Name.Equals(fontName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Check if a specific font is installed (system-wide).
        /// </summary>
        public static bool IsFontInstalled(string fontName)
        {
            using var installedFonts = new InstalledFontCollection();
            return installedFonts.Families.Any(f =>
                f.Name.Equals(fontName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Apply the preferred font to a form and all its controls recursively.
        /// Preserves existing font size and style.
        /// </summary>
        public static void ApplyToForm(System.Windows.Forms.Control control)
        {
            try
            {
                // Only change if the control uses a font that needs replacement
                if (control.Font != null && ShouldReplaceFontFamily(control.Font.FontFamily.Name))
                {
                    control.Font = CreateFont(FontFamily, control.Font.Size, control.Font.Style, control.Font.Unit);
                }

                // Recursively apply to child controls
                foreach (System.Windows.Forms.Control child in control.Controls)
                {
                    ApplyToForm(child);
                }
            }
            catch
            {
                // Ignore font application errors
            }
        }

        /// <summary>
        /// Check if font family should be replaced with fallback.
        /// </summary>
        private static bool ShouldReplaceFontFamily(string familyName)
        {
            // Replace JetBrains Mono if it's not available
            if (familyName.StartsWith("JetBrains", StringComparison.OrdinalIgnoreCase))
            {
                return !IsFontAvailable("JetBrains Mono");
            }
            return false;
        }
    }
}

