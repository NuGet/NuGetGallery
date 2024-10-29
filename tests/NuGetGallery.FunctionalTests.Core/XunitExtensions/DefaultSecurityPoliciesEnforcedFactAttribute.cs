// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.FunctionalTests.XunitExtensions
{
    public class DefaultSecurityPoliciesEnforcedFactAttribute : FactAttribute
    {
        public DefaultSecurityPoliciesEnforcedFactAttribute(bool runIfEnforced = true)
        {
            if (GalleryConfiguration.Instance.DefaultSecurityPoliciesEnforced != runIfEnforced)
            {
                Skip = string.Format(
                    "Default security policies are {0} configured on the server",
                    !GalleryConfiguration.Instance.DefaultSecurityPoliciesEnforced ? "not" : string.Empty);
            }
        }
    }
}