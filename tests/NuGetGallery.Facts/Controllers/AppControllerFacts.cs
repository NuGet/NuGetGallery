// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Gallery;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class AppControllerFacts
    {
        public class TheGetCurrentUserMethod
        {
            [Fact]
            public void GivenNoActiveUserPrincipal_ItReturnsNull()
            {
                // Arrange
                var ctrl = new TestableAppController();
                ctrl.OwinContext = Fakes.CreateOwinContext();

                // Act
                var user = ctrl.InvokeGetCurrentUser();

                // Assert
                Assert.Null(user);
            }
        }

        public class TestableAppController : AppController
        {
            // Nothing but a concrete class to test an abstract class :)

            public User InvokeGetCurrentUser()
            {
                return GetCurrentUser();
            }
        }
    }
}
