// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Web.Mvc;
using System.Web.Routing;
using Xunit;

namespace NuGetGallery.Routing
{
    public class VersionRouteConstraintFacts
    {
        public class TheMatchMethod
        {
            [Fact]
            public void ReturnsTrueIfVersionIsSemantic()
            {
                var routeValues = new RouteValueDictionary { { "version", "1.0.0-beta" } };
                var constraint = new VersionRouteConstraint();

                var result = constraint.Match(null, null, "version", routeValues, RouteDirection.IncomingRequest);

                Assert.True(result);
            }

            [Fact]
            public void ReturnsTrueIfVersionParameterIsNotInValues()
            {
                var constraint = new VersionRouteConstraint();

                var result = constraint.Match(null, null, "version", new RouteValueDictionary(), RouteDirection.IncomingRequest);

                Assert.True(result);
            }

            [Fact]
            public void ReturnsTrueIfVersionIsOptionalParameter()
            {
                var routeValues = new RouteValueDictionary { { "version", UrlParameter.Optional } };
                var constraint = new VersionRouteConstraint();

                var result = constraint.Match(null, null, "version", routeValues, RouteDirection.IncomingRequest);

                Assert.True(result);
            }

            [Fact]
            public void ReturnsTrueIfVersionIsEmptyString()
            {
                var routeValues = new RouteValueDictionary { { "version", "" } };
                var constraint = new VersionRouteConstraint();

                var result = constraint.Match(null, null, "version", routeValues, RouteDirection.IncomingRequest);

                Assert.True(result);
            }

            [Fact]
            public void ReturnsTrueIfVersionIsNull()
            {
                var routeValues = new RouteValueDictionary { { "version", null } };
                var constraint = new VersionRouteConstraint();

                var result = constraint.Match(null, null, "version", routeValues, RouteDirection.IncomingRequest);

                Assert.True(result);
            }
        }
    }
}