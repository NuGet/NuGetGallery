// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Web.Mvc;
using NuGet.Services.Entities;
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
                ctrl.SetOwinContextOverride(Fakes.CreateOwinContext());

                // Act
                var user = ctrl.InvokeGetCurrentUser();

                // Assert
                Assert.Null(user);
            }
        }

        public class TheJsonMethod : TestContainer
        {
            [Fact]
            public void AllowsJsonRequestBehaviorToBeSpecified()
            {
                // Arrange
                var controller = GetController<TestableAppController>();

                // Act
                var output = controller.Json(HttpStatusCode.BadRequest, null, JsonRequestBehavior.AllowGet);

                // Assert
                Assert.Equal(JsonRequestBehavior.AllowGet, output.JsonRequestBehavior);
            }

            [Fact]
            public void DefaultsToDenyGet()
            {
                // Arrange
                var controller = GetController<TestableAppController>();

                // Act
                var output = controller.Json(HttpStatusCode.BadRequest, null);

                // Assert
                Assert.Equal(JsonRequestBehavior.DenyGet, output.JsonRequestBehavior);
            }
        }

        public class TestableAppController : AppController
        {
            public User InvokeGetCurrentUser()
            {
                return GetCurrentUser();
            }
        }
    }
}
