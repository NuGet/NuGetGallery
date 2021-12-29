// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Web.UI;
using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests.WebUITests.AccountManagement
{
    /// <summary>
    /// Tries to login with a POST request with the credentials retrieved from the data source. Validates that the response has the logged in user name.
    /// priority : p0
    /// </summary>
    public class LogonTest
        : WebTest
    {
        static LogonTest()
        {
            RedirectAssembly("Newtonsoft.Json");
        }

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
            var loggedOnUserNameValidationRule = AssertAndValidationHelper.GetValidationRuleForHtmlTagInnerText(
                HtmlTextWriterTag.Span.ToString(),
                HtmlTextWriterAttribute.Class.ToString(),
                "dropdown-username",
                "NugetTestAccount");
            logonPostRequest.ValidateResponse += loggedOnUserNameValidationRule.Validate;

            yield return logonPostRequest;
        }

        /// <summary>
        /// Source: https://stackoverflow.com/a/32698357
        /// </summary>
        public static void RedirectAssembly(string shortName)
        {
            ResolveEventHandler handler = null;

            handler = (sender, args) =>
            {
                var requestedAssembly = new AssemblyName(args.Name);
                if (requestedAssembly.Name != shortName)
                {
                    return null;
                }

                var current = AppDomain
                    .CurrentDomain
                    .GetAssemblies()
                    .LastOrDefault(x => x.GetName().Name == shortName);

                if (current != null)
                {
                    return current;
                }

                return null;
            };

            AppDomain.CurrentDomain.AssemblyResolve += handler;
        }
    }
}

