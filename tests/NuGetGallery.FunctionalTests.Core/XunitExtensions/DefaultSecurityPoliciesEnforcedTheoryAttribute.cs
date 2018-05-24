// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.FunctionalTests.XunitExtensions
{
    public class DefaultSecurityPoliciesEnforcedTheoryAttribute : TheoryAttribute
    {
        public DefaultSecurityPoliciesEnforcedTheoryAttribute()
        {
            if (!GalleryConfiguration.Instance.DefaultSecurityPoliciesEnforced)
            {
                Skip = "Default security policies are not configured on the server";
            }
        }
    }
}
