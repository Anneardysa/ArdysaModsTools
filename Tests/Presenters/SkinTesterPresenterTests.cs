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
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Models;
using ArdysaModsTools.UI.Presenters;
using Moq;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Presenters;

[TestFixture]
public class SkinTesterPresenterTests
{
    private string _tmp = null!;
    private string _real = null!;
    private FakeView _view = null!;
    private Mock<IConfigService> _config = null!;
    private Mock<IHeroGenerationService> _service = null!;
    private string? _target;
    private Action? _duringBuild;

    private sealed class FakeView : ISkinTesterView
    {
        public List<string> Log { get; } = new();
        public IReadOnlyList<string> ZipPaths { get; set; } = Array.Empty<string>();
        public string HeroesJsonPath { get; set; } = "";
        public bool LiveSandbox { get; set; }
        public void ShowTarget(string? targetPath) { }
        public void AppendLog(string line) => Log.Add(line);
        public void SetBusy(bool busy) { }
    }

    [SetUp]
    public void SetUp()
    {
        _tmp = Path.Combine(Path.GetTempPath(), "amt_skintester_ui_" + Guid.NewGuid().ToString("N"));
        _real = Path.Combine(_tmp, "dota 2 beta");
        foreach (var (rel, text) in new[] { ("game/dota/pak01_dir.vpk", "dir"), ("game/dota/gameinfo_branchspecific.gi", "gi"),
                                            ("game/bin/win64/dota.signatures", "DIGEST:x") })
        {
            string p = Path.Combine(_real, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, text);
        }

        string folder = Path.Combine(_tmp, "ModsPack", "Juggernaut");
        Directory.CreateDirectory(folder);
        string heroesJson = Path.Combine(_tmp, "heroes.json");
        File.WriteAllText(heroesJson,
            "[{\"id\":[6,7],\"used_by_heroes\":\"npc_dota_hero_juggernaut\",\"name\":\"Juggernaut\"," +
            "\"sets\":{\"Default Set\":[\"default_skin.vpk\"]}}]");
        string zip = Path.Combine(folder, "item_weapon_exile_odachi.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
        using (var w = new StreamWriter(z.CreateEntry("index.txt").Open()))
            w.Write("\"27486\"\n{\n\t\"used_by_heroes\"\n\t{\n\t\t\"npc_dota_hero_juggernaut\"\t\t\"1\"\n\t}\n}\n");

        _view = new FakeView { ZipPaths = new[] { zip }, HeroesJsonPath = heroesJson };
        _config = new Mock<IConfigService>();
        _config.Setup(c => c.GetLastTargetPath()).Returns(_real);
        _target = null;
        _duringBuild = null;
        _service = new Mock<IHeroGenerationService>();
        _service.Setup(s => s.GenerateBatchAsync(It.IsAny<string>(),
                It.IsAny<IReadOnlyList<(HeroModel, string)>>(), It.IsAny<Action<string>>(),
                It.IsAny<IProgress<(int, int, string)>?>(), It.IsAny<IProgress<(int, string)>?>(),
                It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<ParticlePick>>?>(),
                It.IsAny<CancellationToken>()))
            .Callback((string target, IReadOnlyList<(HeroModel, string)> _, Action<string> _,
                       IProgress<(int, int, string)>? _, IProgress<(int, string)>? _, IProgress<SpeedMetrics>? _,
                       IReadOnlyDictionary<string, IReadOnlyList<ParticlePick>>? _, CancellationToken _) =>
            {
                _target = target;
                _duringBuild?.Invoke();
            })
            .ReturnsAsync(new OperationResult { Success = true, Message = "Successfully installed 1 hero set(s)" });
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_tmp, true); } catch { }
    }

    private Task Build() =>
        new SkinTesterPresenter(_view, _config.Object, (_, _) => _service.Object).BuildAsync(CancellationToken.None);

    [Test]
    public async Task Build_InstallsIntoTheGame_ByDefault()
    {
        await Build();

        Assert.That(_target, Is.EqualTo(_real));
        Assert.That(_view.Log, Has.Some.Contains("Installed to"));
        Assert.That(Directory.Exists(SkinTesterSandbox.Root(_real)), Is.False, "no sandbox unless asked");
    }

    [Test]
    public async Task LiveSandbox_InstallsIntoTheSandbox_NotTheGame()
    {
        _view.LiveSandbox = true;

        await Build();

        Assert.That(_target, Is.EqualTo(SkinTesterSandbox.Root(_real)));
        Assert.That(File.Exists(Path.Combine(_target!, "game", "dota", "pak01_dir.vpk")), Is.True, "prepared before the build");
        Assert.That(_view.Log, Has.Some.Contains("push it from live.html"));
        Assert.That(_view.Log, Has.None.Contains("WARNING"));
    }

    [Test]
    public async Task LiveSandbox_PrepareRefused_NeverBuilds()
    {
        _view.LiveSandbox = true;
        File.Delete(Path.Combine(_real, "game", "bin", "win64", "dota.signatures"));

        await Build();

        Assert.That(_target, Is.Null);
        Assert.That(_view.Log, Has.Some.StartsWith("FAILED - live sandbox"));
    }

    [Test]
    public async Task LiveSandbox_RealInstallChangedDuringTheBuild_IsCalledOut()
    {
        _view.LiveSandbox = true;
        _duringBuild = () => File.AppendAllText(Path.Combine(_real, "game", "dota", "gameinfo_branchspecific.gi"), " oops");

        await Build();

        Assert.That(_view.Log, Has.Some.Contains("WARNING").And.Contains("gameinfo_branchspecific.gi"));
    }
}
