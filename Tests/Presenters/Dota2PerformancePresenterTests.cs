using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Tests.Helpers;
using ArdysaModsTools.UI.Interfaces;
using ArdysaModsTools.UI.Presenters;
using Moq;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Presenters
{
    [TestFixture]
    public class Dota2PerformancePresenterTests
    {
        private Mock<IDota2PerformanceView> _view = null!;
        private Mock<IAutoexecService> _autoexec = null!;
        private Mock<IMapCleanupService> _cleanup = null!;
        private MapCleanupStatus _status = new(false, false, true);

        [SetUp]
        public void SetUp()
        {
            _view = new Mock<IDota2PerformanceView>();
            _autoexec = new Mock<IAutoexecService>();
            _cleanup = new Mock<IMapCleanupService>();
            _cleanup.Setup(c => c.GetStatusAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => _status);
            _cleanup.Setup(c => c.SetEnabledAsync(It.IsAny<string?>(), It.IsAny<IReadOnlySet<MapCleanupGroup>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _ = new Dota2PerformancePresenter(_view.Object, _autoexec.Object, _cleanup.Object, new TestLogger(), @"C:\dota");
        }

        [Test]
        public void Apply_StripsCleanupKeysFromAutoexec_AndInstallsRequestedGroups()
        {
            Dictionary<string, string>? written = null;
            _autoexec.Setup(a => a.ApplySettingsAsync(It.IsAny<string?>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .Callback((string? _, Dictionary<string, string> s, CancellationToken _) => written = s)
                .Returns(Task.CompletedTask);

            _view.Raise(v => v.OnApplySettingsRequested += null, _view.Object,
                "{\"fps_max\":\"60\",\"amt_remove_foliage\":\"1\",\"amt_remove_props\":\"0\"}");

            Assert.That(written, Is.Not.Null);
            Assert.That(written!.Keys, Is.EquivalentTo(new[] { "fps_max" }));
            _cleanup.Verify(c => c.SetEnabledAsync(@"C:\dota",
                It.Is<IReadOnlySet<MapCleanupGroup>>(g => g.Count == 1 && g.Contains(MapCleanupGroup.Foliage)), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public void Apply_UnchangedCleanup_DoesNotRepack()
        {
            _status = new MapCleanupStatus(true, false, true);

            _view.Raise(v => v.OnApplySettingsRequested += null, _view.Object,
                "{\"fps_max\":\"60\",\"amt_remove_foliage\":\"1\",\"amt_remove_props\":\"0\"}");

            _cleanup.Verify(c => c.SetEnabledAsync(It.IsAny<string?>(), It.IsAny<IReadOnlySet<MapCleanupGroup>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void Delete_TurnsMapCleanupOff()
        {
            _status = new MapCleanupStatus(true, true, true);
            _autoexec.Setup(a => a.DeleteCfgAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            _view.Raise(v => v.OnDeleteCfgRequested += null, _view.Object, EventArgs.Empty);

            _cleanup.Verify(c => c.SetEnabledAsync(@"C:\dota",
                It.Is<IReadOnlySet<MapCleanupGroup>>(g => g.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
            _view.Verify(v => v.SetMapCleanupStatusAsync(It.Is<string>(j => j.Contains("\"sync\":true"))), Times.Once);
        }
    }
}
