// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Authentication
{
    public class ApiScopeEvaluatorFacts
    {
        public class TheEvaluateMethod
        {
            private const string DefaultSubject = "a";

            private static readonly Func<TestablePermissionsEntity, string> DefaultGetSubjectFromEntity = (e) => DefaultSubject;

            private static Func<TestablePermissionsEntity, string> CreateGetSubjectFromEntity(string subject)
            {
                return (e) => subject;
            }

            private ApiScopeEvaluator Setup(Mock<IUserService> mockUserService = null)
            {
                if (mockUserService == null)
                {
                    mockUserService = new Mock<IUserService>();
                }

                return new ApiScopeEvaluator(mockUserService.Object);
            }

            private void AssertResult(ApiScopeEvaluationResult result, User owner, PermissionsCheckResult permissionsCheckResult, bool scopesAreValid)
            {
                Assert.Equal(scopesAreValid, result.ScopesAreValid);
                Assert.Equal(permissionsCheckResult, result.PermissionsCheckResult);
                Assert.True(owner.MatchesUser(result.Owner));
            }

            private void AssertScopesNotValidResult(ApiScopeEvaluationResult result)
            {
                AssertResult(result, owner: null, permissionsCheckResult: PermissionsCheckResult.Unknown, scopesAreValid: false);
            }

            [Fact]
            public void ReturnsForbiddenWhenSubjectIsNotAllowedByScope()
            {
                // Arrange
                var scope = new Scope("notallowed", null);

                var apiScopeEvaluator = Setup();

                // Act
                var result = apiScopeEvaluator.Evaluate(null, new[] { scope }, null, null, DefaultGetSubjectFromEntity, null);

                // Assert
                AssertScopesNotValidResult(result);
            }

            [Fact]
            public void ReturnsForbiddenWhenActionIsNotAllowedByScope()
            {
                // Arrange
                var scope = new Scope(NuGetPackagePattern.AllInclusivePattern, NuGetScopes.PackagePush);

                var apiScopeEvaluator = Setup();

                // Act
                var result = apiScopeEvaluator.Evaluate(null, new[] { scope }, null, null, DefaultGetSubjectFromEntity, NuGetScopes.PackagePushVersion);


                // Assert
                AssertScopesNotValidResult(result);
            }

            public static IEnumerable<object[]> EvaluatesNoScopesAsAllInclusive_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData((IEnumerable<Scope>)null);
                    yield return MemberDataHelper.AsData(Enumerable.Empty<Scope>());
                }
            }

            [Theory]
            [MemberData(nameof(EvaluatesNoScopesAsAllInclusive_Data))]
            public void EvaluatesNoScopesAsLegacy(IEnumerable<Scope> scopes)
            {
                // Arrange
                // A Legacy API Key has
                //
                // 1 - an all-inclusive subject scope
                // 2 - an all-inclusive action scope
                // 3 - a "current user" owner scope

                var evaluator = Setup();

                // Act
                // To guarantee that the scope is evaluated with an all-inclusive subject scope, we must test it on two subjects that are COMPLETELY different.
                // For example, if subjects "a" and "ab" are approved, the subject scope could be "a*". However, if subjects "a" and "b" are approved, the subject scope must be "*", which is what we expect for no scopes.
                foreach (var subject in new[] { "a", "b" })
                {
                    EvaluatesNoScopesAsAllInclusive(evaluator, scopes, subject);
                }
            }

            private void EvaluatesNoScopesAsAllInclusive(ApiScopeEvaluator evaluator, IEnumerable<Scope> scopes, string subject)
            {
                var currentUser = new User { Key = 1 };
                var action = new TestableActionRequiringEntityPermissions(PermissionsRequirement.None, (u, e) => PermissionsCheckResult.Allowed);
                var result = evaluator.Evaluate(currentUser, scopes, action, null, CreateGetSubjectFromEntity(subject), NuGetScopes.All);

                AssertResult(result, currentUser, PermissionsCheckResult.Allowed, scopesAreValid: true);
            }

            public static IEnumerable<object[]> ReturnsCorrectPermissionsCheckResultWhenSubjectAndActionMatches_Data
            {
                get
                {
                    return Enum.GetValues(typeof(PermissionsCheckResult)).Cast<PermissionsCheckResult>().Select(r => new object[] { r });
                }
            }

            [Theory]
            [MemberData(nameof(ReturnsCorrectPermissionsCheckResultWhenSubjectAndActionMatches_Data))]
            public void ReturnsCorrectPermissionsCheckResultWhenSubjectAndActionMatches(PermissionsCheckResult permissionsCheckResult)
            {
                var scope = new Scope(NuGetPackagePattern.AllInclusivePattern, NuGetScopes.All);

                ReturnsCorrectFailureResult(permissionsCheckResult, new[] { scope });
            }

            [Theory]
            [MemberData(nameof(ReturnsCorrectPermissionsCheckResultWhenSubjectAndActionMatches_Data))]
            public void WithMultipleScopesReturnsCorrectFailureResult(PermissionsCheckResult permissionsCheckResult)
            {
                var scopes = new[]
                {
                    new Scope(DefaultSubject, null),
                    new Scope(NuGetPackagePattern.AllInclusivePattern, NuGetScopes.All),
                    new Scope(DefaultSubject, null)
                };

                ReturnsCorrectFailureResult(permissionsCheckResult, scopes);
            }

            private void ReturnsCorrectFailureResult(PermissionsCheckResult permissionsCheckResult, IEnumerable<Scope> scopes)
            {
                // Arrange
                var user = new User("test") { Key = 1 };

                var testableActionMock = new Mock<IActionRequiringEntityPermissions<TestablePermissionsEntity>>();
                testableActionMock
                    .Setup(a => a.CheckPermissions(user, user, It.IsAny<TestablePermissionsEntity>()))
                    .Returns(permissionsCheckResult);

                var mockUserService = new Mock<IUserService>();
                mockUserService.Setup(x => x.FindByKey(user.Key, false)).Returns(user);

                var apiScopeEvaluator = Setup(mockUserService);

                // Act
                var result = apiScopeEvaluator.Evaluate(user, scopes, testableActionMock.Object, null, DefaultGetSubjectFromEntity, NuGetScopes.All);

                // Assert
                AssertResult(result, user, permissionsCheckResult, scopesAreValid: true);
            }

            [Fact]
            public void WithMultipleScopesReturnsCorrectOwners()
            {
                // Arrange
                var currentUser = new User("test");
                var scopeUser = new User("scope1") { Key = 1 };

                var scopes = new[]
                {
                    new Scope(scopeUser.Key, "wrongsubject", "wrongaction"),
                    new Scope(scopeUser.Key, NuGetPackagePattern.AllInclusivePattern, NuGetScopes.All),
                    new Scope(scopeUser.Key, NuGetPackagePattern.AllInclusivePattern, NuGetScopes.All)
                };

                var permissionsCheckResult = PermissionsCheckResult.Allowed;
                var testableActionMock = new Mock<IActionRequiringEntityPermissions<TestablePermissionsEntity>>();
                testableActionMock
                    .Setup(a => a.CheckPermissions(currentUser, It.IsAny<User>(), It.IsAny<TestablePermissionsEntity>()))
                    .Returns(permissionsCheckResult);

                var mockUserService = new Mock<IUserService>();
                mockUserService.Setup(u => u.FindByKey(scopeUser.Key, false)).Returns(scopeUser);

                var apiScopeEvaluator = Setup(mockUserService);

                // Act
                var result = apiScopeEvaluator.Evaluate(currentUser, scopes, testableActionMock.Object, null, DefaultGetSubjectFromEntity, NuGetScopes.All);

                // Assert
                AssertResult(result, scopeUser, permissionsCheckResult, scopesAreValid: true);
            }

            [Fact]
            public void ThrowsIfMultipleOwnerScopes()
            {
                // Arrange
                var scopes =
                    new[] { 535, 212, 6534 }
                        .Select(k => new Scope(k, NuGetPackagePattern.AllInclusivePattern, NuGetScopes.All));

                var permissionsCheckResult = PermissionsCheckResult.Allowed;
                var testableActionMock = new Mock<IActionRequiringEntityPermissions<TestablePermissionsEntity>>();
                testableActionMock
                    .Setup(a => a.CheckPermissions(It.IsAny<User>(), It.IsAny<User>(), It.IsAny<TestablePermissionsEntity>()))
                    .Returns(permissionsCheckResult);

                var apiScopeEvaluator = Setup();

                // Act/Assert
                Assert.Throws<ArgumentException>(() => apiScopeEvaluator.Evaluate(null, scopes, testableActionMock.Object, null, DefaultGetSubjectFromEntity, NuGetScopes.All));
            }
        }
    }
}
