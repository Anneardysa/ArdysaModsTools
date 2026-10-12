using ArdysaModsTools.UI.Forms;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class MiscFormAlertScriptTests
    {
        [Test]
        public void BuildAlertScript_KeepsBackslashPathsQuotesAndNewlines()
        {
            var script = MiscFormWebView.BuildAlertScript(
                "Generation Failed",
                "Can't write.\n\nSee %LocalAppData%\\ArdysaModsTools\\ardysa_fallback.log",
                "error");

            Assert.That(script, Is.EqualTo(
                @"showAlert('Generation Failed', 'Can\'t write.\n\nSee %LocalAppData%\\ArdysaModsTools\\ardysa_fallback.log', 'error')"));
        }

        [Test]
        public void BuildAlertScript_DropsCarriageReturnsAndEscapesEveryArgument()
        {
            var script = MiscFormWebView.BuildAlertScript("It's", "a\r\nb", "in'fo");

            Assert.That(script, Is.EqualTo(@"showAlert('It\'s', 'a\nb', 'in\'fo')"));
        }
    }
}
