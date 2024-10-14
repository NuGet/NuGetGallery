// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Ganss.Xss;

namespace NuGetGallery.Helpers
{
    public static class HtmlSanitizerWrapper
    {
        private static readonly HtmlSanitizer Sanitizer;

        static HtmlSanitizerWrapper()
        {
            Sanitizer = new HtmlSanitizer();
        }

        public static string SanitizeText(string input)
        {
            if (!string.IsNullOrWhiteSpace(input))
            {
                return Sanitizer.Sanitize(input);
            }
            return input;
        }
    }
}
