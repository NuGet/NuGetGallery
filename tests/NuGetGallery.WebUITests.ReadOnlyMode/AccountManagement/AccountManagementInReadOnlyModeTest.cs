// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests.WebUITests.ReadOnlyMode
{
    /// <summary>
    /// Go Account management activities like "Unsubscribe" notications and "Reset" Api key in read-only mode and check for error.
    /// </summary>
    public class AccountManagementInReadOnlyModeTest : WebTest
    {
        public AccountManagementInReadOnlyModeTest()
        {
            PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            // Do initial login to be able to perform package management.
            var logonGet = AssertAndValidationHelper.GetLogonGetRequest();
            yield return logonGet;

            var logonPost = AssertAndValidationHelper.GetLogonPostRequest(this);
            yield return logonPost;

            var accountPageRequest = new WebTestRequest(UrlHelper.AccountPageUrl);
            var extractionRule1 = AssertAndValidationHelper.GetDefaultExtractHiddenFields();
            accountPageRequest.ExtractValues += extractionRule1.Extract;
            yield return accountPageRequest;


            var unsubscribeRequest = new WebTestRequest(UrlHelper.AccountUnscribeUrl);
            unsubscribeRequest.Method = "POST";
            unsubscribeRequest.ExpectedHttpStatusCode = 503;
            var unsubscribeRequestBody = new FormPostHttpBody();
            unsubscribeRequestBody.FormPostParameters.Add("__RequestVerificationToken", Context["$HIDDEN1.__RequestVerificationToken"].ToString());
            unsubscribeRequestBody.FormPostParameters.Add("emailAllowed", "false");
            unsubscribeRequest.Body = unsubscribeRequestBody;

            // Check for read-only status.
            var readonlyValidationRule = AssertAndValidationHelper.GetValidationRuleForFindText(Constants.ReadOnlyModeError);
            unsubscribeRequest.ValidateResponse += readonlyValidationRule.Validate;
            yield return unsubscribeRequest;

            var resetApiKeyRequest = new WebTestRequest(UrlHelper.AccountApiKeyResetUrl);
            resetApiKeyRequest.Method = "POST";
            resetApiKeyRequest.ExpectedHttpStatusCode = 503;

            var resetApiKeyRequestBody = new FormPostHttpBody();
            resetApiKeyRequestBody.FormPostParameters.Add("__RequestVerificationToken", Context["$HIDDEN1.__RequestVerificationToken"].ToString());
            resetApiKeyRequest.Body = resetApiKeyRequestBody;

            // Check for read-only error
            resetApiKeyRequest.ValidateResponse += readonlyValidationRule.Validate;
            yield return resetApiKeyRequest;
        }
    }
}
