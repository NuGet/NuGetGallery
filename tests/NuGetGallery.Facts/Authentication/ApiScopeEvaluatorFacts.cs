using Moq;
using NuGetGallery.Framework;
using NuGetGallery.Infrastructure;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.Authentication
{
    public class ApiScopeEvaluatorFacts
    {
        public class TheEvaluateMethod
        {
            private const string DefaultSubject = "a";

            private static Func<TestablePermissionsEntity, string> DefaultGetSubjectFromEntity = (e) => DefaultSubject;

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

            private void AssertResult(bool scopesAreValid, PermissionsCheckResult permissionsCheckResult, User owner, ApiScopeEvaluationResult result)
            {
                Assert.Equal(true, result.ScopesAreValid);
                Assert.Equal(permissionsCheckResult, result.PermissionsCheckResult);
                Assert.True(owner.MatchesUser(result.Owner));
            }

            private void AssertScopesNotValidResult(ApiScopeEvaluationResult actual)
            {
                AssertResult(false, PermissionsCheckResult.Unknown, null, actual);
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
                    yield return MemberDataHelper.AsData((IEnumerable<Scope>) null);
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

                var currentUser = new User { Key = 1 };
                var mockUserService = new Mock<IUserService>();
                mockUserService.Setup(x => x.FindByKey(currentUser.Key)).Returns(currentUser);

                var evaluator = Setup(mockUserService);

                // Act
                // To guarantee that the scope is evaluated with an all-inclusive subject scope, we must test it on two subjects that are COMPLETELY different.
                // For example, if subjects "a" and "ab" are approved, the subject scope could be "a*". However, if subjects "a" and "b" are approved, the subject scope must be "*", which is what we expect for no scopes.
                foreach (var subject in new[] { "a", "b" })
                {
                    EvaluatesNoScopesAsAllInclusive(evaluator, currentUser, scopes, subject);
                }
            }

            private void EvaluatesNoScopesAsAllInclusive(ApiScopeEvaluator evaluator, User currentUser, IEnumerable<Scope> scopes, string subject)
            {
                var action = new TestableActionRequiringEntityPermissions(PermissionsRequirement.None, (u, e) => PermissionsCheckResult.Allowed);
                var result = evaluator.Evaluate(currentUser, scopes, action, null, CreateGetSubjectFromEntity(subject), NuGetScopes.All);

                AssertResult(true, PermissionsCheckResult.Allowed, currentUser, result);
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
                mockUserService.Setup(x => x.FindByKey(user.Key)).Returns(user);

                var apiScopeEvaluator = Setup(mockUserService);

                // Act
                var result = apiScopeEvaluator.Evaluate(user, scopes, testableActionMock.Object, null, DefaultGetSubjectFromEntity, NuGetScopes.All);

                // Assert
                AssertResult(true, permissionsCheckResult, user, result);
            }

            [Fact]
            public void WithMultipleScopesReturnsCorrectOwners()
            {
                // Arrange
                var currentUser = new User("test");
                var scopeUser1 = new User("scope1") { Key = 1 };
                var scopeUser2 = new User("scope2") { Key = 2 };

                var scopes = new[]
                {
                    new Scope("wrongsubject", null),
                    new Scope(scopeUser1.Key, NuGetPackagePattern.AllInclusivePattern, NuGetScopes.All),
                    new Scope(scopeUser2.Key, NuGetPackagePattern.AllInclusivePattern, NuGetScopes.All)
                };

                var permissionsCheckResult = PermissionsCheckResult.Allowed;
                var testableActionMock = new Mock<IActionRequiringEntityPermissions<TestablePermissionsEntity>>();
                testableActionMock
                    .Setup(a => a.CheckPermissions(currentUser, It.IsAny<User>(), It.IsAny<TestablePermissionsEntity>()))
                    .Returns(permissionsCheckResult);

                var mockUserService = new Mock<IUserService>();
                mockUserService.Setup(u => u.FindByKey(scopeUser1.Key)).Returns(scopeUser1);
                mockUserService.Setup(u => u.FindByKey(scopeUser2.Key)).Returns(scopeUser2);

                var apiScopeEvaluator = Setup(mockUserService);

                // Act
                var result = apiScopeEvaluator.Evaluate(currentUser, scopes, testableActionMock.Object, null, DefaultGetSubjectFromEntity, NuGetScopes.All);

                // Assert
                AssertResult(true, permissionsCheckResult, scopeUser1, result);
            }
        }
    }
}
