// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NuGetGallery.FunctionalTests.Helpers
{
    public static class AssertAndValidationHelper
    {
        /// <summary>
        /// Extracts form data needed for login from the login page HTML content
        /// </summary>
        /// <param name="htmlContent">HTML content of the login page</param>
        /// <returns>Dictionary of form parameters for login</returns>
        public static Dictionary<string, string> GetLogonPostFormData(string htmlContent)
        {
            // Extract the request verification token
            var tokenMatch = Regex.Match(htmlContent,
                @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)"" />");

            if (!tokenMatch.Success || tokenMatch.Groups.Count < 2)
            {
                throw new InvalidOperationException("Could not extract request verification token from login page");
            }

            var token = tokenMatch.Groups[1].Value;

            var formData = new Dictionary<string, string>
            {
                { "__RequestVerificationToken", token },
                { "ReturnUrl", "/" },
                { "LinkingAccount", "false" },
                { Constants.UserNameOrEmailFormField, GalleryConfiguration.Instance.Account.Email },
                { Constants.PasswordFormField, GalleryConfiguration.Instance.Account.Password }
            };

            return formData;
        }
    }
}

