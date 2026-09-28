// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Options;

namespace NuGet.Services.CDNRedirect.Configuration
{
    public class RedirectOptionsValidator : IValidateOptions<RedirectOptions>
    {
        public ValidateOptionsResult Validate(string? name, RedirectOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.RedirectDestination))
            {
                return ValidateOptionsResult.Fail("The RedirectDestination configuration value is required.");
            }

            if (!Uri.TryCreate(options.RedirectDestination, UriKind.Absolute, out var destinationUri)
                || !string.Equals(destinationUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrEmpty(destinationUri.Query)
                || !string.IsNullOrEmpty(destinationUri.Fragment))
            {
                return ValidateOptionsResult.Fail("The RedirectDestination configuration value must be an HTTPS base URL without a query or fragment.");
            }

            return ValidateOptionsResult.Success;
        }
    }
}
