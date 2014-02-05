using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGetGallery.FunctionTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAutomation;

namespace NuGetGallery.FunctionalTests.Fluent
{

    [TestClass]
    public class LogonTest : NuGetFluentTest 
    {

        [TestMethod]
        public void Logon()
        {
            LogonHelper("/");
            LogonHelper("/packages");
            LogonHelper("/stats");
            LogonHelper("/policies/Contact");
            LogonHelper("/policies/Terms");
            LogonHelper("/policies/Privacy");
        }

        private void LogonHelper(string page)
        {
            I.Open(UrlHelper.BaseUrl + page);
            I.Expect.Url(x => x.AbsoluteUri.Contains(page));

            string registerSignIn = "a:contains('Register / Sign in')";
            string signOut = "a:contains('Sign out')";
            string expectedUserName = "a:contains('NugetTestAccount')";

            I.Click(registerSignIn);
            I.Expect.Url(x => x.LocalPath.Contains("LogOn"));
            I.Enter(EnvironmentSettings.TestAccountName).In("#SignIn_UserNameOrEmail");
            I.Enter(EnvironmentSettings.TestAccountPassword).In("#SignIn_Password");
            I.Click("#signin-link");

            I.Expect.Url(x => x.AbsoluteUri.Contains(page));
            I.Expect.Count(0).Of(registerSignIn);
            I.Expect.Count(1).Of(signOut);
            I.Expect.Count(1).Of(expectedUserName);
            I.Click(signOut);

            I.Expect.Url(x => x.AbsoluteUri.Contains(page));
            I.Expect.Count(1).Of(registerSignIn);
            I.Expect.Count(0).Of(signOut);
            I.Expect.Count(0).Of(expectedUserName);
        }
    }
}
