using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenQA.Selenium;
using Xunit;

namespace FunctionalTests
{
    public class TheHomePage : FunctionalTest
    {
        [Fact]
        public void ShouldRenderSuccessfully()
        {
            Driver.Navigate().GoToUrl(SiteRoot);
            Assert.True(Driver
                .FindElements(By.TagName("h1"))
                .Any(e =>
                    e.Text.Equals("What is NuGet?", StringComparison.Ordinal)));
        }

        [Fact]
        public void ShouldHaveNoBrokenImages()
        {
            Driver.Navigate().GoToUrl(SiteRoot);
            Assert.False(Driver
                .FindElements(By.TagName("img"))
                .Any(i =>
                    i.Size.IsEmpty ||
                    (i.Size.Height == 0 && i.Size.Width == 0)));
        }
    }
}
