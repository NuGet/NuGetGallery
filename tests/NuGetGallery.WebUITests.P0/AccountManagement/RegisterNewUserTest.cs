// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests.WebUITests.AccountManagement
{
    /// <summary>
    /// Sends http POST request to register a new user and checks that a pending confirmation page is shown as response.
    /// priority : p0
    /// </summary>
    public class RegisterNewUserTest : WebTest
    {
        public RegisterNewUserTest()
        {
            PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            WebTestRequest registerPageRequest = AssertAndValidationHelper.GetHttpRequestForUrl(UrlHelper.LogonPageUrl);
            yield return registerPageRequest;

            WebTestRequest registerPagePostRequest = new WebTestRequest(UrlHelper.RegisterPageUrl);
            registerPagePostRequest.Method = "POST";
            registerPagePostRequest.ExpectedResponseUrl = UrlHelper.RegistrationPendingPageUrl;

            //create a form and set the UserName, Email and password as form post parameters.
            //We just need to set some unique user name and Email.
            FormPostHttpBody registerNewUserFormPost = new FormPostHttpBody();
            registerNewUserFormPost.FormPostParameters.Add("__RequestVerificationToken", Context["$HIDDEN1.__RequestVerificationToken"].ToString());
            registerNewUserFormPost.FormPostParameters.Add("LinkingAccount", "false");
            registerNewUserFormPost.FormPostParameters.Add(Constants.EmailAddressFormField, DateTime.Now.Ticks + "@live.com"); //add a dummy mail account.
            registerNewUserFormPost.FormPostParameters.Add(Constants.UserNameFormField, DateTime.Now.Ticks + "NewAccount");
            registerNewUserFormPost.FormPostParameters.Add(Constants.RegisterPasswordFormField, "Xxxxxxxx123");
            registerPagePostRequest.Body = registerNewUserFormPost;

            // Validate the response to make sure that it has the confirmation text in it.
            var pendingConfirmationTextRule = AssertAndValidationHelper.GetValidationRuleForFindText(Constants.RegisterNewUserConfirmationText);
            registerPagePostRequest.ValidateResponse += pendingConfirmationTextRule.Validate;
            yield return registerPagePostRequest;
        }
    }
}


