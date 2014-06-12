using Microsoft.VisualStudio.TestTools.WebTesting;
using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
using NuGetGallery.FunctionalTests.Helpers;
using System;
using System.Collections.Generic;
using System.Web.UI;

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
            this.PreAuthenticate = true;
        }
        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            //Do initial login
            WebTestRequest logonGet = AssertAndValidationHelper.GetLogonGetRequest();
            yield return logonGet;
            logonGet = null;

            WebTestRequest logonPostRequest = AssertAndValidationHelper.GetLogonPostRequest(this);
            ValidateHtmlTagInnerText loggedOnUserNameValidationRule;
            loggedOnUserNameValidationRule = AssertAndValidationHelper.GetValidationRuleForHtmlTagInnerText(HtmlTextWriterTag.A.ToString(), HtmlTextWriterAttribute.Href.ToString(), "/account", "NugetTestAccount");             
            logonPostRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(loggedOnUserNameValidationRule.Validate);

            yield return logonPostRequest;
            logonPostRequest = null;
        }
    }
}

