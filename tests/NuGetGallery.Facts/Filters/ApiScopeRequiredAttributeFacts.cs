// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Autofac;
using NuGetGallery.Services.Authentication;
using Xunit;

namespace NuGetGallery.Filters
{
    public class ApiScopeRequiredAttributeFacts
    {
        public class TheScopeActionsProperty
        {
            [Fact]
            public void NotOverwrittenByAutofacPropertyInjection()
            {
                // Arrange
                var autofacBuilder = new ContainerBuilder();
                autofacBuilder.RegisterInstance(new ApiScopeRequiredAttribute(NuGetScopes.PackagePush))
                    .AsSelf()
                    .PropertiesAutowired(); // enable property injection
                var container = autofacBuilder.Build();

                // Act
                var resolvedAttribute = container.Resolve<ApiScopeRequiredAttribute>();

                // Assert
                Assert.Single(resolvedAttribute.ScopeActions);
                Assert.Equal(NuGetScopes.PackagePush, resolvedAttribute.ScopeActions.First());
            }
        }
    }
}