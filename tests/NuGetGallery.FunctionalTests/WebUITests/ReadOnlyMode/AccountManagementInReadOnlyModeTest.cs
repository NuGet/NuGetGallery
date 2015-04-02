using Microsoft.VisualStudio.TestTools.WebTesting;
using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
using NuGetGallery.FunctionalTests.Helpers;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.Collections.Generic;

namespace NuGetGallery.FunctionalTests
{
    /// <summary>
    /// Go Account management activities like "Unsunscribe" notications and "Reset" Api key in read-only mode and check for error.
    /// </summary>
    public class AccountManagementInReadOnlyModeTest : WebTest
    {
        public AccountManagementInReadOnlyModeTest()
        {
            this.PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            //run this test only if read-only mode is set. This is to avoid false failures while doing Run all tests locally.
            if (EnvironmentSettings.ReadOnlyMode.Equals("True", StringComparison.OrdinalIgnoreCase))
            {
                //Do initial login to be able to perform package management.
                WebTestRequest logonGet = AssertAndValidationHelper.GetLogonGetRequest();
                yield return logonGet;
                logonGet = null;
                WebTestRequest logonPost = AssertAndValidationHelper.GetLogonPostRequest(this);
                yield return logonPost;
                logonPost = null;

                WebTestRequest accountPageRequest = new WebTestRequest(UrlHelper.AccountPageUrl);
                ExtractHiddenFields extractionRule1 = AssertAndValidationHelper.GetDefaultExtractHiddenFields();
                accountPageRequest.ExtractValues += new EventHandler<ExtractionEventArgs>(extractionRule1.Extract);
                yield return accountPageRequest;
                accountPageRequest = null;

                WebTestRequest unsubscribeRequest = new WebTestRequest(UrlHelper.AccountUnscribeUrl);
                unsubscribeRequest.Method = "POST";
                unsubscribeRequest.ExpectedHttpStatusCode = 503;
                FormPostHttpBody unsubscribeRequestBody = new FormPostHttpBody();
                unsubscribeRequestBody.FormPostParameters.Add("__RequestVerificationToken", this.Context["$HIDDEN1.__RequestVerificationToken"].ToString());
                unsubscribeRequest.Body = unsubscribeRequestBody;
                //check for read-only status.     
                ValidationRuleFindText readonlyValidationRule = AssertAndValidationHelper.GetValidationRuleForFindText(Constants.ReadOnlyModeError);
                unsubscribeRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(readonlyValidationRule.Validate);
                yield return unsubscribeRequest;
                unsubscribeRequest = null;

                WebTestRequest resetApiKeyRequest = new WebTestRequest(UrlHelper.AccountApiKeyResetUrl);
                resetApiKeyRequest.Method = "POST";
                resetApiKeyRequest.ExpectedHttpStatusCode = 503;
                FormPostHttpBody resetApiKeyRequestBody = new FormPostHttpBody();
                resetApiKeyRequestBody.FormPostParameters.Add("__RequestVerificationToken", this.Context["$HIDDEN1.__RequestVerificationToken"].ToString());
                resetApiKeyRequest.Body = resetApiKeyRequestBody;
                //Check for read-only error     
                resetApiKeyRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(readonlyValidationRule.Validate);
                yield return resetApiKeyRequest;
                resetApiKeyRequest = null;
            }
        }
    }
}
