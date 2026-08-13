// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
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

        public class TheHaveEqualScopesWithSameAllowedActionAndSubjectMethod
        {
            public static IEnumerable<object[]> Scopes_Data
            {
                get
                {
                    yield return new object[] { null, null, true };
                    yield return new object[] { null, new List<Scope> { new Scope("subject1", "allowedAction1") }, false };
                    yield return new object[] { new List<Scope> { new Scope("subject1", "allowedAction1") }, null, false };
                    yield return new object[] { new List<Scope> { new Scope("subject1", "allowedAction1") },
                                                new List<Scope> { new Scope("subject1", "allowedAction1"),
                                                                  new Scope("subject1", "allowedAction2") },
                                                false };
                    yield return new object[] { new List<Scope> { new Scope("subject1", "allowedAction1"),
                                                                  new Scope("subject1", "allowedAction2"),
                                                                  new Scope("subject2", "allowedAction1") },
                                                new List<Scope> { new Scope("subject1", "allowedAction1"),
                                                                  new Scope("subject1", "allowedAction2"),
                                                                  new Scope("subject2", "allowedAction1") },
                                                true };
                    yield return new object[] { new List<Scope> { new Scope("subject1", "allowedAction1"),
                                                                  new Scope("subject2", "allowedAction1"),
                                                                  new Scope("subject1", "allowedAction2"), },
                                                new List<Scope> { new Scope("subject1", "allowedAction2"),
                                                                  new Scope("subject1", "allowedAction1"),
                                                                  new Scope("subject2", "allowedAction1") },
                                                true };
                    yield return new object[] { new List<Scope> { new Scope("subject1", "allowedAction1"),
                                                                  new Scope("subject2", "allowedAction1"),
                                                                  new Scope("subject1", "allowedAction2"), },
                                                new List<Scope> { new Scope("subject1", "allowedAction2"),
                                                                  new Scope("subject1", "allowedAction1"),
                                                                  new Scope("subject3", "allowedAction1") },
                                                false };
                    yield return new object[] { new List<Scope> { new Scope("subject1", "allowedAction1"),
                                                                  new Scope("subject2", "allowedAction1"),
                                                                  new Scope("subject1", "allowedAction2"), },
                                                new List<Scope> { new Scope("subject1", "allowedAction3"),
                                                                  new Scope("subject1", "allowedAction1"),
                                                                  new Scope("subject3", "allowedAction1") },
                                                false };
                    yield return new object[] { new List<Scope> { new Scope("subject1", "allowedAction1") },
                                                new List<Scope> { new Scope("subject1", "ALLOWEDACTION1") },
                                                false };
                    yield return new object[] { new List<Scope> { new Scope("subject1", "allowedAction1") },
                                                new List<Scope> { new Scope("SUBJECT1", "allowedAction1") },
                                                false };
                }
            }

            [Theory]
            [MemberData(nameof(Scopes_Data))]
            public void HaveEqualScopesWithSameAllowedActionAndSubject(IEnumerable<Scope> scopes1, IEnumerable<Scope> scopes2, bool expected)
            {
                Assert.Equal(expected, scopes1.HaveEqualScopesWithSameAllowedActionAndSubject(scopes2));
            }
        }
    }
}
