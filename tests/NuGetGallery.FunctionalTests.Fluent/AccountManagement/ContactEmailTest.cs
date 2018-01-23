// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.Fluent.AccountManagement
{
    public class ContactEmailTest
        : NuGetFluentTest
    {
        public ContactEmailTest(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [Fact(Skip = "Mail Check is not happening correctly at this time")]
        [Description("Verify the gallery options for reporting abuse.")]
        [Priority(2)]
        public async Task ContactEmailAbuse()
        {
            var packageName = "NuGetGallery.FunctionalTests.Fluent.ContactEmailTest";
            var version = "1.0.0";
            var subject = string.Empty;
            var received = false;

            await UploadPackageIfNecessary(packageName, version);

            // Send an abuse report for the package.
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName);
            I.Click("a:contains('Report Abuse')");
            I.Enter("testnuget@gmail.com").In("#Email");
            I.Select("Other").From("#Reason");
            I.Enter(GetMessage()).In("#Message");
            I.Enter("testnuget").In("#Signature");
            I.Click("input[value='Report']");

            // Validate in-site message to owner
            I.Expect.Url(x => x.AbsoluteUri.EndsWith("/packages/" + packageName + "/" + version));
            I.Expect.Count(1).Of(@"p:contains('Your abuse report has been sent to the gallery operators')");

            // Validate owner receives a copy of the message
            // Wait for up to 5 mins. to make sure that the mail is delivered properly.
            for (int i = 0; ((i < 10) && !received); i++)
            {
                System.Threading.Thread.Sleep(30000);
                subject = string.Empty;
                var mailHelper = new MailHelper();
                received = mailHelper.IsMailSentForAbuseReport(packageName, version, "Other", out subject);
            }

            var userMessage = string.Format("Abuse report not sent to the owner properly. Actual subject : {0}", subject);
            Assert.True(received, userMessage);
        }

        [Fact(Skip = "Mail Check is not happening correctly at this time")]
        [Description("Verify the gallery options for contacting owners.")]
        [Priority(2)]
        public void ContactEmailOwners()
        {
            var packageName = "NuGetGallery.FunctionalTests.Fluent.ContactEmailTest";
            var subject = string.Empty;

            // Contact the package's owners.
            I.LogOn(EnvironmentSettings.TestAccountEmail, EnvironmentSettings.TestAccountPassword);
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName);
            I.Click("a:contains('Contact Owners')");
            I.Enter(GetMessage()).In("#Message");
            I.Click("input[value='Send']");

            // Validate in-site message to owner
            I.Expect.Url(x => x.AbsoluteUri.EndsWith("/packages/" + packageName));
            I.Expect.Count(1).Of(@"p:contains('Your message has been sent to the owners of " + packageName + "')");

            // Validate owner receives a copy of the message
            // Wait for 30 secs. to make sure that the mail is delivered properly.
            var received = false;
            for (int i = 0; ((i < 10) && !received); i++)
            {
                System.Threading.Thread.Sleep(30000);
                subject = string.Empty;
                var mailHelper = new MailHelper();
                received = mailHelper.IsMailSentForContactOwner(packageName, out subject);
            }

            var userMessage = string.Format("Owner not contacted correctly. Actual subject : {0}", subject);
            Assert.True(received, userMessage);
        }

        [Fact(Skip = "Mail Check is not happening correctly at this time")]
        [Description("Verify the gallery options for contacting us.")]
        [Priority(2)]
        public void ContactEmailSupport()
        {
            var packageName = "NuGetGallery.FunctionalTests.Fluent.ContactEmailTest";
            var version = "1.0.0";
            var subject = string.Empty;

            // Contact support.
            I.LogOn(EnvironmentSettings.TestAccountEmail, EnvironmentSettings.TestAccountPassword);
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName);
            I.Click("a:contains('Contact Support')");
            I.Select("Other").From("#Reason");
            I.Enter(GetMessage()).In("#Message");
            I.Click("input[value='Report']");

            // Validate in-site message to owner
            I.Expect.Url(x => x.AbsoluteUri.EndsWith("/packages/" + packageName + "/" + version));
            I.Expect.Count(1).Of(@"p:contains('Your support request has been sent to the gallery operators')");

            // Validate owner receives a copy of the message
            // Wait for 30 secs. to make sure that the mail is delivered properly.
            var received = false;
            for (int i = 0; ((i < 10) && !received); i++)
            {
                System.Threading.Thread.Sleep(30000);
                subject = string.Empty;
                var mailHelper = new MailHelper();
                received = mailHelper.IsMailSentForContactSupport(packageName, version, "Other", out subject);
            }

            var userMessage = string.Format("Owner not contacted correctly. Actual subject : {0}", subject);
            Assert.True(received, userMessage);

        }

        private static string GetMessage()
        {
            var sb = new StringBuilder();
            sb.AppendLine("TEST TEST TEST TEST TEST TEST TEST TEST");
            sb.AppendLine("");
            sb.AppendLine("This message was created as part of an");
            sb.AppendLine("automated test used by the NuGet team.");
            sb.AppendLine("Please ignore this message.");
            sb.AppendLine("");
            sb.AppendLine("TEST TEST TEST TEST TEST TEST TEST TEST");
            return sb.ToString();
        }
    }
}
