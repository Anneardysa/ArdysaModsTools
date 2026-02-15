/*
 * Copyright (C) 2026 Ardysa
 * Licensed under GPL v3
 */

using System.IO;
using System.Runtime.InteropServices;

namespace ArdysaModsTools.Installer.Services
{
    /// <summary>
    /// Installs bundled fonts (JetBrains Mono) to the Windows Fonts directory.
    /// Mirrors the font installation from the original Inno Setup script.
    /// 
    /// Fonts are installed as "install-if-not-present" and never removed on uninstall,
    /// matching the InnoSetup flags: onlyifdoesntexist uninsneveruninstall
    /// </summary>
    public static class FontInstaller
    {
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern int AddFontResource(string lpFileName);

        private const int WM_FONTCHANGE = 0x001D;
        private const int HWND_BROADCAST = 0xFFFF;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Installs JetBrains Mono fonts from the Assets/Fonts directory
        /// to %WINDIR%\Fonts if they don't already exist.
        /// </summary>
        public static void InstallFonts(string installPath)
        {
            var fontsDir = Path.Combine(installPath, "Assets", "Fonts");
            if (!Directory.Exists(fontsDir))
                return;

            var systemFontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            var installed = false;

            foreach (var fontFile in Directory.GetFiles(fontsDir, "*.ttf"))
            {
                var fileName = Path.GetFileName(fontFile);
                var targetPath = Path.Combine(systemFontsDir, fileName);

                // Only install if not already present (matches InnoSetup onlyifdoesntexist)
                if (File.Exists(targetPath))
                    continue;

                try
                {
                    File.Copy(fontFile, targetPath, overwrite: false);
                    AddFontResource(targetPath);

                    // Register in registry
                    var fontName = GetFontDisplayName(fileName);
                    Microsoft.Win32.Registry.SetValue(
                        @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts",
                        fontName,
                        fileName);

                    installed = true;
                }
                catch
                {
                    // Non-critical — font just won't be available system-wide
                }
            }

            // Notify other applications about font changes
            if (installed)
            {
                SendMessage((IntPtr)HWND_BROADCAST, WM_FONTCHANGE, IntPtr.Zero, IntPtr.Zero);
            }
        }

        // ════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════════

        private static string GetFontDisplayName(string fileName)
        {
            // Map known font files to their Windows Fonts display names
            return fileName switch
            {
                "JetBrainsMono-Regular.ttf" => "JetBrains Mono (TrueType)",
                "JetBrainsMono-Bold.ttf" => "JetBrains Mono Bold (TrueType)",
                "JetBrainsMono-Italic.ttf" => "JetBrains Mono Italic (TrueType)",
                "JetBrainsMono-BoldItalic.ttf" => "JetBrains Mono Bold Italic (TrueType)",
                "JetBrainsMono-Light.ttf" => "JetBrains Mono Light (TrueType)",
                "JetBrainsMono-Medium.ttf" => "JetBrains Mono Medium (TrueType)",
                "JetBrainsMono-SemiBold.ttf" => "JetBrains Mono SemiBold (TrueType)",
                "JetBrainsMono-ExtraBold.ttf" => "JetBrains Mono ExtraBold (TrueType)",
                "JetBrainsMono-ExtraLight.ttf" => "JetBrains Mono ExtraLight (TrueType)",
                "JetBrainsMono-Thin.ttf" => "JetBrains Mono Thin (TrueType)",
                _ => $"{Path.GetFileNameWithoutExtension(fileName)} (TrueType)"
            };
        }
    }
}
