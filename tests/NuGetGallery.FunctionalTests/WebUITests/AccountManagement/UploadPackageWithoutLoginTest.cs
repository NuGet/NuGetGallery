
namespace NuGetGallery.FunctionalTests
{
    using Microsoft.VisualStudio.TestTools.WebTesting;
    using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
    using NuGetGallery.FunctionTests.Helpers;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Web.UI;

    /// <summary>
    /// Tries to hit upload package page without logging in and checks if the log on form is displayed in the response.
    /// </summary>
    public class UploadPackageWithoutLoginTest : WebTest
    {
        public UploadPackageWithoutLoginTest()
        {
            this.PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {            
            WebTestRequest uploadPackageRequest = new WebTestRequest(UrlHelper.UploadPageUrl);
            uploadPackageRequest.ExpectedResponseUrl = UrlHelper.LogonPageUrlOnPackageUpload;
            ValidateHtmlTagInnerText logOnFormValidationRule = AssertAndValidationHelper.GetValidationRuleForHtmlTagInnerText(HtmlTextWriterTag.Label.ToString(), HtmlTextWriterAttribute.For.ToString(), "SignIn_UserNameOrEmail", "Username or Email");               
            uploadPackageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(logOnFormValidationRule.Validate);          
            yield return uploadPackageRequest;
            uploadPackageRequest = null;
        }
    }
}
