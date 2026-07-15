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
using System.IO;
using System.Text.Json;

namespace ArdysaModsTools.UI.Helpers
{
    public static class WebViewTheming
    {
        private const string StyleId = "amt-theme";
        private const string HtmlAnchor = "<html lang=\"en\"";
        private const string HeadAnchor = "</head>";

        private static string? _cssCache;

        public static string Apply(string html)
        {
            if (string.IsNullOrEmpty(html)) return html;

            string css = ReadCss();
            if (css.Length > 0)
            {
                int head = html.IndexOf(HeadAnchor, StringComparison.OrdinalIgnoreCase);
                if (head >= 0)
                    html = html.Insert(head, $"<style id=\"{StyleId}\">\n{css}\n</style>\n");
            }

            if (!Theme.IsDarkMode)
            {
                int tag = html.IndexOf(HtmlAnchor, StringComparison.OrdinalIgnoreCase);
                if (tag >= 0)
                    html = html.Insert(tag + HtmlAnchor.Length, " data-theme=\"light\"");
            }

            return html;
        }

        public static string BuildBootstrapScript()
        {
            string css = JsonSerializer.Serialize(ReadCss());
            return $"(function(){{var s=document.createElement('style');s.id='{StyleId}';s.textContent={css};" +
                   $"document.documentElement.appendChild(s);}})();{SetThemeScript()}";
        }

        public static string SetThemeScript()
            => Theme.IsDarkMode
                ? "document.documentElement.removeAttribute('data-theme');"
                : "document.documentElement.setAttribute('data-theme','light');";

        private static string ReadCss()
        {
            if (_cssCache != null) return _cssCache;
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Html", "theme.css");
                _cssCache = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            }
            catch
            {
                _cssCache = string.Empty;
            }
            return _cssCache;
        }
    }
}
