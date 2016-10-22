// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using FluentAutomation.Interfaces;

namespace NuGetGallery.FunctionalTests.Fluent
{
    public static class ExtensionMethods
    {
        public static void LogOn(this IActionSyntaxProvider I, string userName, string password)
        {
            I.Open(UrlHelper.BaseUrl + "users/account/LogOn");
            I.Expect.Url(x => x.LocalPath.Contains("LogOn"));
            I.Enter(EnvironmentSettings.TestAccountName).In("#SignIn_UserNameOrEmail");
            I.Enter(EnvironmentSettings.TestAccountPassword).In("#SignIn_Password");
            I.Click("#signin-link");
            I.Wait(1);
        }

        public static void UploadPackageUsingUI(this IActionSyntaxProvider I, string fullPackagePath)
        {
            // Navigate to the Upload Package page.  This will fail if the user never uploaded the previous package, hence the error handling.
            I.Open(string.Format(UrlHelper.UploadPageUrl));
            try
            {
                I.Expect.Url(x => x.AbsoluteUri.Contains("/packages/manage/Upload"));
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
