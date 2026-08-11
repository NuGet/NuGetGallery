// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Net;
using NuGetGallery.Authentication;
using NuGetGallery.Filters;
using Xunit;

namespace NuGetGallery
{
    public class StagingControllerFacts
    {
        [Fact]
        public void RequiresAuthenticationAndStagingScope()
        {
            var attributes = typeof(StagingController).GetCustomAttributes(inherit: true);

            Assert.Contains(attributes, x => x is ApiAuthorizeAttribute);
            var scope = Assert.IsType<ApiScopeRequiredAttribute>(
                attributes.Single(x => x is ApiScopeRequiredAttribute));
            Assert.Equal(new[] { NuGetScopes.PackageStage }, scope.ScopeActions);
        }

        [Fact]
        public void ReturnsStableUnavailableErrorUntilImplementationLands()
        {
            var controller = new StagingController();

            var result = Assert.IsType<StagingJsonResult>(controller.ListStagingGroups());
            var response = Assert.IsType<StagingApiErrorResponse>(result.Value);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
            Assert.Equal(StagingApiErrorCodes.StagingUnavailable, response.Error.Code);
        }

    }
}
