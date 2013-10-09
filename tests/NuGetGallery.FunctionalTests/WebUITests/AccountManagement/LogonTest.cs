
namespace NuGetGallery.FunctionalTests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.WebTesting;
    using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
    using System.Web.UI;
    using NuGetGallery.FunctionTests.Helpers;
    using NuGetGallery.FunctionalTests.TestBase;

    /// <summary>
    /// Tries to login with a POST request with the credentials retrieved from the data source. Validates that the response has the logged in user name.
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
            if (UrlHelper.BaseUrl.Contains("qa.nugettest.org")) {
                loggedOnUserNameValidationRule = AssertAndValidationHelper.GetValidationRuleForHtmlTagInnerText(HtmlTextWriterTag.A.ToString(), HtmlTextWriterAttribute.Href.ToString(), "/account", "TestNuGetAccount");             
            }
            else 
            {
                loggedOnUserNameValidationRule = AssertAndValidationHelper.GetValidationRuleForHtmlTagInnerText(HtmlTextWriterTag.A.ToString(), HtmlTextWriterAttribute.Href.ToString(), "/account", "NugetTestAccount");             
            }
            logonPostRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(loggedOnUserNameValidationRule.Validate);
            
            yield return logonPostRequest;
            logonPostRequest = null;          
        }
    }
}
