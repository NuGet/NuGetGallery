using FluentAutomation.Interfaces;
using NuGetGallery.FunctionTests.Helpers;
using System;

namespace NuGetGallery.FunctionalTests.Fluent
{
    public static class ExtensionMethods  
    {
        public static void LogOn(this INativeActionSyntaxProvider I, string userName, string password)
        {
            I.Open(UrlHelper.BaseUrl + "users/account/LogOn");
            I.Expect.Url(x => x.LocalPath.Contains("LogOn"));
            I.Enter(EnvironmentSettings.TestAccountName).In("#SignIn_UserNameOrEmail");
            I.Enter(EnvironmentSettings.TestAccountPassword).In("#SignIn_Password");
            I.Click("#signin-link");
            I.Wait(1);
        }

        public static void UploadPackageUsingUI(this INativeActionSyntaxProvider I, string fullPackagePath)
        {
            // Navigate to the Upload Package page.  This will fail if the user never uploaded the previous package, hence the error handling.
            I.Open(String.Format(UrlHelper.UploadPageUrl));
            try
            {
                I.Expect.Url(x => x.AbsoluteUri.Contains("/packages/Upload"));
            }
            catch
            {
                I.Click("a[class='cancel']");
            }

            // Upload the package.
            I.Click("input[name='UploadFile']");
            I.Wait(5);
            I.Type(fullPackagePath);
            I.Press("{ENTER}");
            I.Wait(5);
            I.Click("input[value='Upload']");
        }
    }
}
