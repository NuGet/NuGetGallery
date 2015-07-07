// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.VisualStudio.TestTools.WebTesting;

namespace NuGetGallery.FunctionalTests.Helpers
{
    /// <summary>
    /// Validation rule for matching text in response headers.
    /// </summary>
    public class ValidationRuleFindHeaderText
        : ValidationRule
    {
        private readonly string _findText;

        public ValidationRuleFindHeaderText(string findText)
        {
            _findText = findText;
        }

        public override void Validate(object sender, ValidationEventArgs e)
        {
            e.IsValid = e.Response.Headers.ToString().Contains(_findText);
        }
    }
}
