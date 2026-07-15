using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services.Misc
{
    [TestFixture]
    public class PerformancePresetInvariantTests
    {
        [Test]
        public void EveryPreset_WithGlobalShadowsOn_UsesAutoShadowTextures()
        {
            var html = File.ReadAllText(FindPerformanceHtml());
            var presets = ExtractPresets(html);

            Assert.That(presets, Is.Not.Empty, "Failed to parse any presets from dota2_performance.html");

            foreach (var (name, cvars) in presets)
            {
                if (!cvars.TryGetValue("cl_globallight_shadow_mode", out var modeRaw)) continue;
                if (!int.TryParse(modeRaw, out var mode) || mode < 2) continue;

                foreach (var key in new[] { "lb_shadow_texture_width_override", "lb_shadow_texture_height_override" })
                {
                    Assert.That(cvars.TryGetValue(key, out var val), Is.True,
                        $"Preset '{name}' sets shadow mode {mode} but is missing {key}.");
                    Assert.That(val, Is.EqualTo("-1"),
                        $"Preset '{name}' has cl_globallight_shadow_mode={mode} with {key}={val}. " +
                        "Mode >=2 requires auto shadow textures (-1); a fixed 128px atlas blacks out the map on unit hover.");
                }
            }
        }

        private static IReadOnlyList<(string Name, Dictionary<string, string> Cvars)> ExtractPresets(string html)
        {
            var body = ExtractBraceBlock(html, "const PRESETS =");
            var result = new List<(string, Dictionary<string, string>)>();

            foreach (Match block in Regex.Matches(body, @"(\w+)\s*:\s*\{([^{}]*)\}"))
            {
                var name = block.Groups[1].Value;
                var cvars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match kv in Regex.Matches(block.Groups[2].Value, "(\\w+)\\s*:\\s*\"([^\"]*)\""))
                    cvars[kv.Groups[1].Value] = kv.Groups[2].Value;
                result.Add((name, cvars));
            }
            return result;
        }

        private static string ExtractBraceBlock(string text, string marker)
        {
            var start = text.IndexOf(marker, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Marker '{marker}' not found.");
            var open = text.IndexOf('{', start);
            Assert.That(open, Is.GreaterThanOrEqualTo(0), $"Opening brace after '{marker}' not found.");

            var depth = 0;
            for (var i = open; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}' && --depth == 0)
                    return text.Substring(open + 1, i - open - 1);
            }
            throw new AssertionException($"Unbalanced braces after '{marker}'.");
        }

        private static string FindPerformanceHtml()
        {
            const string rel = "Assets/Html/dota2_performance.html";
            for (var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); dir != null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, rel);
                if (File.Exists(candidate)) return candidate;
            }
            throw new FileNotFoundException($"Could not locate '{rel}' above the test directory.");
        }
    }
}
