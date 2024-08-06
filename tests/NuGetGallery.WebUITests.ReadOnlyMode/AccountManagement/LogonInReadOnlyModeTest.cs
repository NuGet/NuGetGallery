// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests.WebUITests.ReadOnlyMode
{
    /// <summary>
    /// Tries to login with a POST request to a read-only server. This is not allowed since logging on updates the
    /// user record in the database.
    /// </summary>
    public class LogonTest
        : WebTest
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
            logonPostRequest.ExpectedHttpStatusCode = 503;
            var readOnlyModeTextRule = AssertAndValidationHelper.GetValidationRuleForFindText(Constants.ReadOnlyModeError);
            logonPostRequest.ValidateResponse += readOnlyModeTextRule.Validate;

            yield return logonPostRequest;
        }
    }
}
