using Moq;
using NuGetGallery.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.Authentication
{
    public class ApiScopeEvaluatorFacts
    {
        private class TestableScopeSubjectConverter : IScopeSubjectConverter<TestablePermissionsEntity>
        {
            public string Result { get; set; }

            public TestableScopeSubjectConverter(string result)
            {
                Result = result;
            }

            public string ConvertToScopeSubject(TestablePermissionsEntity subject)
            {
                return Result;
            }
        }

        public class TheEvaluateMethod
        {
            private ApiScopeEvaluator Setup(
                TestableScopeSubjectConverter testableScopeSubjectConverter = null,
                IUserService userService = null)
            {
                if (testableScopeSubjectConverter == null)
                {
                    testableScopeSubjectConverter = new TestableScopeSubjectConverter("packageId");
                }

                if (userService == null)
                {
                    userService = new Mock<IUserService>().Object;
                }

                var typeToScopeSubjectEvaluator = new Dictionary<Type, object>
                {
                    { typeof(TestablePermissionsEntity), testableScopeSubjectConverter }
                };

                return new ApiScopeEvaluator(userService, typeToScopeSubjectEvaluator);
            }

            [Fact]
            public void ReturnsForbiddenWhenSubjectIsNotAllowedByScope()
            {
                // Arrange
                var scope = new Scope("a", null);
                var scopeSubjectConverter = new TestableScopeSubjectConverter("b");

                var apiScopeEvaluator = Setup(scopeSubjectConverter);

                // Act
                var result = apiScopeEvaluator.Evaluate<TestablePermissionsEntity>(null, new[] { scope }, null, null, out var owner, null);

                // Assert
                Assert.Equal(ApiScopeEvaluationResult.Forbidden, result);
            }

            [Fact]
            public void ReturnsForbiddenWhenActionIsNotAllowedByScope()
            {
                // Arrange
                var scope = new Scope(NuGetPackagePattern.AllInclusivePattern, NuGetScopes.PackagePush);

                var apiScopeEvaluator = Setup();

                // Act
                var result = apiScopeEvaluator.Evaluate<TestablePermissionsEntity>(null, new[] { scope }, null, null, out var owner, NuGetScopes.PackagePushVersion);


                // Assert
                Assert.Equal(ApiScopeEvaluationResult.Forbidden, result);
            }

            public static IEnumerable<object[]> ReturnsResultOfActionWhenSubjectAndActionMatches_Data
            {
                get
                {
                    return Enum.GetValues(typeof(PermissionsCheckResult)).Cast<PermissionsCheckResult>().Select(r => new object[] { r });
                }
            }

            [Theory]
            [MemberData(nameof(ReturnsResultOfActionWhenSubjectAndActionMatches_Data))]
            public void ReturnsResultOfActionWhenSubjectAndActionMatches(PermissionsCheckResult permissionsCheckResult)
            {
                // Arrange
                var user = new User("test");
                var scope = new Scope(NuGetPackagePattern.AllInclusivePattern, NuGetScopes.All);

                var testableActionMock = new Mock<IActionRequiringEntityPermissions<TestablePermissionsEntity>>();
                testableActionMock
                    .Setup(a => a.CheckPermissions(user, user, It.IsAny<TestablePermissionsEntity>()))
                    .Returns(permissionsCheckResult);

                ApiScopeEvaluationResult expectedApiScopeEvaluationResult;
                switch (permissionsCheckResult)
                {
                    case PermissionsCheckResult.AccountFailure:
                    case PermissionsCheckResult.PackageRegistrationFailure:
                    case PermissionsCheckResult.Unknown:
                        expectedApiScopeEvaluationResult = ApiScopeEvaluationResult.Forbidden;
                        break;
                    case PermissionsCheckResult.ReservedNamespaceFailure:
                        expectedApiScopeEvaluationResult = ApiScopeEvaluationResult.ConflictReservedNamespace;
                        break;
                    case PermissionsCheckResult.Allowed:
                        expectedApiScopeEvaluationResult = ApiScopeEvaluationResult.Success;
                        break;
                    default:
                        throw new ArgumentException($"Invalid {nameof(PermissionsCheckResult)} provided!");
                }

                var apiScopeEvaluator = Setup();
                
                // Act
                var actualApiScopeEvaluationResult = apiScopeEvaluator.Evaluate(user, new[] { scope }, testableActionMock.Object, null, out var owner, NuGetScopes.All);

                // Assert
                Assert.Equal(expectedApiScopeEvaluationResult, actualApiScopeEvaluationResult);
                if (expectedApiScopeEvaluationResult == ApiScopeEvaluationResult.Success)
                {
                    Assert.Equal(user.Username, owner.Username);
                }
                else
                {
                    Assert.Equal(null, owner);
                }
            }

            [Theory]
            [InlineData(PermissionsCheckResult.AccountFailure, ApiScopeEvaluationResult.Forbidden)]
            [InlineData(PermissionsCheckResult.PackageRegistrationFailure, ApiScopeEvaluationResult.Forbidden)]
            [InlineData(PermissionsCheckResult.Unknown, ApiScopeEvaluationResult.Forbidden)]
            [InlineData(PermissionsCheckResult.ReservedNamespaceFailure, ApiScopeEvaluationResult.ConflictReservedNamespace)]
            public void WithMultipleScopesReturnsCorrectFailureResult(PermissionsCheckResult permissionsCheckResult, ApiScopeEvaluationResult expectedApiScopeEvaluationResult)
            {
                // Arrange
                var user = new User("test");
                var scopes = new[]
                {
                    new Scope("a", null),
                    new Scope(NuGetPackagePattern.AllInclusivePattern, NuGetScopes.All),
                    new Scope("a", null)
                };

                var testableActionMock = new Mock<IActionRequiringEntityPermissions<TestablePermissionsEntity>>();
                testableActionMock
                    .Setup(a => a.CheckPermissions(user, user, It.IsAny<TestablePermissionsEntity>()))
                    .Returns(permissionsCheckResult);

                var apiScopeEvaluator = Setup();

                // Act
                var result = apiScopeEvaluator.Evaluate(user, scopes, testableActionMock.Object, null, out var owner, NuGetScopes.All);

                // Assert
                Assert.Equal(expectedApiScopeEvaluationResult, result);
                Assert.Equal(null, owner);
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
                    new Scope("a", null),
                    new Scope(scopeUser1.Key, NuGetPackagePattern.AllInclusivePattern, NuGetScopes.All),
                    new Scope(scopeUser2.Key, NuGetPackagePattern.AllInclusivePattern, NuGetScopes.All)
                };

                var testableActionMock = new Mock<IActionRequiringEntityPermissions<TestablePermissionsEntity>>();
                testableActionMock
                    .Setup(a => a.CheckPermissions(currentUser, It.IsAny<User>(), It.IsAny<TestablePermissionsEntity>()))
                    .Returns(PermissionsCheckResult.Allowed);

                var userServiceMock = new Mock<IUserService>();
                userServiceMock.Setup(u => u.FindByKey(scopeUser1.Key)).Returns(scopeUser1);
                userServiceMock.Setup(u => u.FindByKey(scopeUser2.Key)).Returns(scopeUser2);

                var apiScopeEvaluator = Setup(userService: userServiceMock.Object);

                // Act
                var result = apiScopeEvaluator.Evaluate(currentUser, scopes, testableActionMock.Object, null, out var owner, NuGetScopes.All);

                // Assert
                Assert.Equal(ApiScopeEvaluationResult.Success, result);
                Assert.True(scopeUser1.MatchesUser(owner));
            }
        }
    }
}
