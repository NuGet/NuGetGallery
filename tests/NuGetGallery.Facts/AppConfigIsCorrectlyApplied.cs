using System;
using System.Configuration;
using Xunit;

namespace NuGetGallery
{
    public class AppConfigIsCorrectlyApplied
    {
        [Fact]
        public void VerifyAppDomainHasConfigurationSettings()
        {
            string value = ConfigurationManager.AppSettings["YourTestsAreNotGoingInsane"];
            Assert.False(String.IsNullOrEmpty(value), "App.Config not loaded");
        }
    }
}
