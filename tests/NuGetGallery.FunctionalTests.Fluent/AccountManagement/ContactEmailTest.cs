using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;
using System.Text;

namespace NuGetGallery.FunctionalTests.Fluent
{
    [TestClass]
    public class ContactEmailTest : NuGetFluentTest
    {
        [TestMethod]
        [Description("Verify the gallery options for reporting abuse.")]
        [Priority(2)]
        public void ContactEmailAbuse()
        {
            string packageName = "NuGetGallery.FunctionalTests.Fluent.ContactEmailTest";
            string version = "1.0.0";
            string subject = "";
            bool received = false;

            UploadPackageIfNecessary(packageName, version);

            // Send an abuse report for the package.
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName);
            I.Click("a:contains('Report Abuse')");
            I.Enter("testnuget@gmail.com").In("#Email");
            I.Select("Other").From("#Reason");
            I.Enter(GetMessage()).In("#Message");
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
                received = MailHelper.IsMailSentForAbuseReport(packageName, version, "Other", out subject);
            }
            Assert.IsTrue(received, "Abuse report not sent to the owner properly. Actual subject : {0}", subject);
        }

        [TestMethod]
        [Description("Verify the gallery options for contacting owners.")]
        [Priority(2)]
        public void ContactEmailOwners()
        {
            string packageName = "NuGetGallery.FunctionalTests.Fluent.ContactEmailTest";
            string subject = "";
            bool received = false;

            // Contact the package's owners.
            I.LogOn(EnvironmentSettings.TestAccountName, EnvironmentSettings.TestAccountPassword);
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName);
            I.Click("a:contains('Contact Owners')");
            I.Enter(GetMessage()).In("#Message");
            I.Click("input[value='Send']");

            // Validate in-site message to owner
            I.Expect.Url(x => x.AbsoluteUri.EndsWith("/packages/" + packageName));
            I.Expect.Count(1).Of(@"p:contains('Your message has been sent to the owners of " + packageName + "')");

            // Validate owner receives a copy of the message
            // Wait for 30 secs. to make sure that the mail is delivered properly.
            received = false;
            for (int i = 0; ((i < 10) && !received); i++)
            { 
                System.Threading.Thread.Sleep(30000);
                subject = string.Empty;
                received = MailHelper.IsMailSentForContactOwner(packageName, out subject);
            }
            Assert.IsTrue(received, "Owner not contacted correctly. Actual subject : {0}", subject);
        }

        [TestMethod]
        [Description("Verify the gallery options for contacting us.")]
        [Priority(2)]
        public void ContactEmailSupport()
        {
            string packageName = "NuGetGallery.FunctionalTests.Fluent.ContactEmailTest";
            string version = "1.0.0";
            string subject = "";
            bool received = false;

            // Contact support.
            I.LogOn(EnvironmentSettings.TestAccountName, EnvironmentSettings.TestAccountPassword);
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
            received = false;
            for (int i = 0; ((i < 10) && !received); i++)
            { 
                System.Threading.Thread.Sleep(30000);
                subject = string.Empty;
                received = MailHelper.IsMailSentForContactSupport(packageName, version, "Other", out subject);
            }
            Assert.IsTrue(received, "Owner not contacted correctly. Actual subject : {0}", subject);

        }

        private string GetMessage(){
            StringBuilder sb = new StringBuilder();
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
