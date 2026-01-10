using NUnit.Framework;

namespace ArdysaModsTools.Tests
{
    /// <summary>
    /// Global test setup and utilities.
    /// </summary>
    [SetUpFixture]
    public class GlobalSetup
    {
        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            // Global test initialization
            // Set up any test-wide resources here
        }

        [OneTimeTearDown]
        public void RunAfterAllTests()
        {
            // Global test cleanup
        }
    }
}
