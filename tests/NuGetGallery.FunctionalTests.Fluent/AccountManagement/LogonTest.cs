using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;

namespace NuGetGallery.FunctionalTests.Fluent
{
    [TestClass]
    public class LogonTest : NuGetFluentTest 
    {
        [TestMethod]
        [Description("Verify staying at the same starting pages after logged on and signed out, and the logon/signout links")]
        [Priority(1)]
        public void Logon()
        {
            LogonHelper("/");
            // Added for checking the account page after logged in - equivalent of clicking the username after logged in
            LogonHelper("/account");
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
