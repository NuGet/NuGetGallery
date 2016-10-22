// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.Fluent.AccountManagement
{

    public class ResendConfirmationTest : NuGetFluentTest
    {
        public ResendConfirmationTest(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [Fact(Skip = "Test Account is already confirmed")]
        [Description("Covers scenarios around the Resend Confirmation link.")]
        [Priority(2)]
        public void ResendConfirmation()
        {
            I.LogOn(EnvironmentSettings.TestAccountName, EnvironmentSettings.TestAccountPassword);

            // Go to account confirmation page
            I.Open(UrlHelper.BaseUrl + "account/confirmationrequired");
            // Click the (change) link
            I.Open(UrlHelper.BaseUrl + "account");
            I.Expect.Count(0).Of("h1:contains('404')");

            // Go back to account confirmation page and click the Send Confirmation Email link
            I.Open(UrlHelper.BaseUrl + "account/confirmationrequired");
            I.Click("input[value='Send Confirmation Email']");
            I.Expect.Count(1).Of("h2:contains('Confirmation Email Sent!')");
            I.Expect.Count(0).Of("h1:contains('404')");
        }
    }
}
