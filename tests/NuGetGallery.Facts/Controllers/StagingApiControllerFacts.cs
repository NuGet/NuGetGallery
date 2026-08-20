// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Authentication;
using NuGetGallery.Filters;
using Xunit;

namespace NuGetGallery
{
    public class StagingApiControllerFacts
    {
        [Fact]
        public void ControllerRequiresApiAuthorization()
        {
            Assert.NotEmpty(typeof(StagingApiController).GetCustomAttributes(typeof(ApiAuthorizeAttribute), inherit: true));
            Assert.NotEmpty(typeof(StagingApiController).GetCustomAttributes(typeof(ApiScopeRequiredAttribute), inherit: true));
        }
    }
}
