// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using NuGetGallery.Services.Authentication;
using Xunit;

namespace NuGetGallery.Extensions
{
    public class ScopeExtensionsFacts
    {
        public class TheAllowsActionMethod
        {
            [Fact]
            public void WhenScopeIsNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() =>
                {
                    ScopeExtensions.AllowsActions(null, NuGetScopes.PackagePush);
                });
            }

            [Fact]
            public void WhenActionsIsNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() =>
                {
                    new Scope().AllowsActions((string[])null);
                });
            }

            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public void WhenRequestedActionIsNullOrEmpty_ReturnsTrue(string requestedAction)
            {
                var scope = new Scope(1234, "subject", "action");

                Assert.True(scope.AllowsActions(requestedAction));
            }

            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public void WhenScopeActionIsNullOrEmpty_ReturnsTrue(string scopeAction)
            {
                var scope = new Scope(1234, "subject", scopeAction);

                Assert.True(scope.AllowsActions("action"));
            }

            [Theory]
            [InlineData("action")]
            [InlineData("ACTion")]
            public void WhenScopeActionEquals_ReturnsTrue(string requestedAction)
            {
                var scope = new Scope(1234, "subject", "action");

                Assert.True(scope.AllowsActions(requestedAction));
            }

            [Theory]
            [InlineData("all")]
            [InlineData("ALL")]
            public void WhenScopeActionIsAll_ReturnsTrue(string requestedAction)
            {
                var scope = new Scope(1234, "subject", NuGetScopes.All);

                Assert.True(scope.AllowsActions(requestedAction));
            }
        }

        public class TheAllowsSubjectMethod
        {
            [Fact]
            public void WhenScopeIsNull_ThrowsArgumentNull()
            {
                Assert.Throws<ArgumentNullException>(() =>
                {
                    ScopeExtensions.AllowsSubject(null, "subject");
                });
            }

            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public void WhenSubjectIsNullOrEmpty_ThrowsArgumentNull(string subject)
            {
                Assert.Throws<ArgumentNullException>(() =>
                {
                    ScopeExtensions.AllowsSubject(new Scope(), subject);
                });
            }

            [Theory]
            [InlineData("SomePackage", "SomePackage")]
            [InlineData("somepackage", "SomePackage")]
            [InlineData("*", "SomePackage")]
            [InlineData("Microsoft.*.Abstract", "Microsoft.Configuration.Abstract")]
            public void WhenSubjectMatches_ReturnsTrue(string scopeSubject, string requestedSubject)
            {
                var scope = new Scope(1234, scopeSubject, "action");

                Assert.True(scope.AllowsSubject(requestedSubject));
            }

            [Theory]
            [InlineData("SomePackage", "SomeOtherPackage")]
            [InlineData("Microsoft.*.Abstract", "Microsoft.Configuration")]
            [InlineData("%@~!>^/\"*", "Microsoft.Configuration")]
            public void WhenSubjectDoesNotMatch_ReturnsFalse(string scopeSubject, string requestedSubject)
            {
                var scope = new Scope(1234, scopeSubject, "action");

                Assert.False(scope.AllowsSubject(requestedSubject));
            }
        }

        public class TheHasOwnerScopeMethod
        {
            [Fact]
            public void WhenScopeIsNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() =>
                {
                    ScopeExtensions.HasOwnerScope(null);
                });
            }

            [Fact]
            public void WhenHasOwnerScope_ReturnsTrue()
            {
                var scope = new Scope(1234, "subject", "action");

                Assert.True(scope.HasOwnerScope());
            }

            [Fact]
            public void WhenHasNoOwnerScope_ReturnsFalse()
            {
                var scope = new Scope((User)null, "subject", "action");

                Assert.False(scope.HasOwnerScope());
            }
        }
    }
}
