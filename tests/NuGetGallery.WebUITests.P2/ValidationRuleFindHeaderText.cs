// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.VisualStudio.TestTools.WebTesting;
using System;

namespace NuGetGallery.FunctionalTests.Helpers
{
    /// <summary>
    /// Validation rule for matching text in response headers.
    /// </summary>
    public class ValidationRuleFindHeaderText
        : ValidationRule
    {
        private readonly string _findText;
        private readonly StringComparison _stringComparison;

        public ValidationRuleFindHeaderText(string findText) : this(findText, StringComparison.Ordinal)
        {
            _findText = findText;
        }

        public ValidationRuleFindHeaderText(string findText, StringComparison stringComparison)
        {
            _findText = findText;
            _stringComparison = stringComparison;
        }

        public override void Validate(object sender, ValidationEventArgs e)
        {
            e.IsValid = e.Response.Headers.ToString().IndexOf(_findText, _stringComparison) >= 0;
        }
    }
}
