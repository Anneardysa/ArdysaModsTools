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
            var source = File.ReadAllText(FindPerformanceDataTs());
            var presets = ExtractPresets(source);

            Assert.That(presets, Is.Not.Empty, "Failed to parse any presets from data.ts");

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

        [Test]
        public void EveryPreset_KeepsFpsCapsAtOrAbove60()
        {
            foreach (var (name, cvars) in ExtractPresets(File.ReadAllText(FindPerformanceDataTs())))
            {
                var fps = int.Parse(cvars["fps_max"]);
                Assert.That(fps == 0 || fps >= 60, Is.True, $"Preset '{name}' caps fps_max at {fps} (< 60).");
                Assert.That(int.Parse(cvars["fps_max_ui"]), Is.GreaterThanOrEqualTo(60), $"Preset '{name}' caps fps_max_ui below 60.");
            }
        }

        [Test]
        public void EveryPreset_WithReducedRenderScale_EnablesFsr()
        {
            foreach (var (name, cvars) in ExtractPresets(File.ReadAllText(FindPerformanceDataTs())))
            {
                var scale = double.Parse(cvars["mat_viewportscale"], System.Globalization.CultureInfo.InvariantCulture);
                var expected = scale < 1.0 ? "1" : "0";
                Assert.That(cvars.TryGetValue("r_dota_fsr_upsample", out var fsr) ? fsr : null, Is.EqualTo(expected),
                    $"Preset '{name}' has mat_viewportscale={scale}; r_dota_fsr_upsample must be {expected}.");
            }
        }

        [Test]
        public void EveryPreset_DefinesTheSameCvars()
        {
            var presets = ExtractPresets(File.ReadAllText(FindPerformanceDataTs()));
            var reference = presets[0].Cvars.Keys;
            foreach (var (name, cvars) in presets)
                Assert.That(cvars.Keys, Is.EquivalentTo(reference), $"Preset '{name}' defines a different cvar set than '{presets[0].Name}'.");
        }

        private static IReadOnlyList<(string Name, Dictionary<string, string> Cvars)> ExtractPresets(string source)
        {
            var body = ExtractBraceBlock(source, "const PRESETS");
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

        private static string FindPerformanceDataTs()
        {
            const string rel = "Frontend/src/pages/dota2_performance/data.ts";
            for (var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); dir != null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, rel);
                if (File.Exists(candidate)) return candidate;
            }
            throw new FileNotFoundException($"Could not locate '{rel}' above the test directory.");
        }
    }
}
