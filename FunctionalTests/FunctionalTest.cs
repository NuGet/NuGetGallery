using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.IE;
using OpenQA.Selenium.PhantomJS;

namespace FunctionalTests
{
    public abstract class FunctionalTest : IDisposable
    {
        public IWebDriver Driver { get; set; }
        public Uri SiteRoot { get; set; }

        public FunctionalTest()
        {
            SiteRoot = new Uri("https://www.nuget.org");

            Driver = new PhantomJSDriver();
        }

        public string Resolve(string relative, string query = null)
        {
            return new UriBuilder(SiteRoot)
            {
                Path = relative,
                Query = query
            }.Uri.AbsoluteUri;
        }

        public void Dispose()
        {
            Driver.Quit();
        }
    }
}
