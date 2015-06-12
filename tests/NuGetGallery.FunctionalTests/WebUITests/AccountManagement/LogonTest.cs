using System.Collections.Generic;
using System.Web.UI;
using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests
{
    /// <summary>
    /// Tries to login with a POST request with the credentials retrieved from the data source. Validates that the response has the logged in user name.
    /// priority : p0
    /// </summary>
    public class LogonTest : WebTest
    {
        public LogonTest()
        {
            PreAuthenticate = true;
        }
        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            //Do initial login
            WebTestRequest logonGet = AssertAndValidationHelper.GetLogonGetRequest();
            yield return logonGet;

            WebTestRequest logonPostRequest = AssertAndValidationHelper.GetLogonPostRequest(this);
            var loggedOnUserNameValidationRule = AssertAndValidationHelper.GetValidationRuleForHtmlTagInnerText(HtmlTextWriterTag.A.ToString(), HtmlTextWriterAttribute.Href.ToString(), "/account", "NugetTestAccount");
            logonPostRequest.ValidateResponse += loggedOnUserNameValidationRule.Validate;

            yield return logonPostRequest;
        }
    }
}

