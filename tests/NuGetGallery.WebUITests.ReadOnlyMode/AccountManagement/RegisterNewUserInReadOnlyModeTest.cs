// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests.WebUITests.ReadOnlyMode
{
    /// <summary>
    /// Sends http POST request to register a new user in read-only mode and checks if read-only mode error is shown properly.
    /// </summary>
    public class RegisterNewUserInReadOnlyModeTest : WebTest
    {
        public RegisterNewUserInReadOnlyModeTest()
        {
            PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            //run this test only if read-only mode is set. This is to avoid false failures while doing Run all tests locally.
            if (EnvironmentSettings.ReadOnlyMode.Equals("True", StringComparison.OrdinalIgnoreCase))
            {
                var registerPageRequest = AssertAndValidationHelper.GetHttpRequestForUrl(UrlHelper.LogonPageUrl);
                yield return registerPageRequest;

                var registerPagePostRequest = new WebTestRequest(UrlHelper.RegisterPageUrl);
                registerPagePostRequest.Method = "POST";
                registerPagePostRequest.ExpectedResponseUrl = UrlHelper.RegistrationPendingPageUrl;

                // Create a form and set the UserName, Email and password as form post parameters.
                // We just need to set some unique user name and Email.
                var registerNewUserFormPost = new FormPostHttpBody();
                registerNewUserFormPost.FormPostParameters.Add("__RequestVerificationToken", Context["$HIDDEN1.__RequestVerificationToken"].ToString());
                registerNewUserFormPost.FormPostParameters.Add("LinkingAccount", "false");
                registerNewUserFormPost.FormPostParameters.Add(Constants.EmailAddressFormField, DateTime.Now.Ticks + "@live.com"); //add a dummy mail account. This will be fixed once we incorporate the logic to delete user.
                registerNewUserFormPost.FormPostParameters.Add(Constants.UserNameFormField, DateTime.Now.Ticks + "NewAccount");
                registerNewUserFormPost.FormPostParameters.Add(Constants.RegisterPasswordFormField, "xxXxx1xx");
                registerPagePostRequest.Body = registerNewUserFormPost;
                registerPagePostRequest.ExpectedHttpStatusCode = 503;
                // Validate the response to make sure that it shows the error message for read-only mode.
                var readOnlyModeTextRule = AssertAndValidationHelper.GetValidationRuleForFindText(Constants.ReadOnlyModeError);
                registerPagePostRequest.ValidateResponse += readOnlyModeTextRule.Validate;
                yield return registerPagePostRequest;
            }
        }
    }
}


