using System;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using NuGetGallery.Framework;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery.Filters
{
    public class ApiKeyAuthorizeAttributeFacts : TestContainer
    {
        [Fact]
        public void UsesCredentialTableToFindUser()
        {
            ApiKeyAuthorizeAttribute attribute = CreateAttribute();
            var apiKey = Guid.NewGuid();
            var mockFilterContext = CreateActionFilterContext(apiKey.ToString());

            GetMock<IUserService>()
                .Setup(us => us.AuthenticateCredential(
                    Constants.CredentialTypes.ApiKeyV1,
                    apiKey.ToString().ToLowerInvariant()))
                .Returns(new Credential() { User = Fakes.Owner });

            // Act
            attribute.OnActionExecuting(mockFilterContext.Object);

            // Assert
            Assert.Null(mockFilterContext.Object.Result);
        }

        [Fact]
        public void UsesApiKeyColumnToFindUserIfNoRecordInCredentialTable()
        {
            ApiKeyAuthorizeAttribute attribute = CreateAttribute();
            var apiKey = Guid.NewGuid();
            var mockFilterContext = CreateActionFilterContext(apiKey.ToString());

            GetMock<IUserService>()
                .Setup(us => us.FindByApiKey(apiKey))
                .Returns(Fakes.Owner);

            // Act
            attribute.OnActionExecuting(mockFilterContext.Object);

            // Assert
            Assert.Null(mockFilterContext.Object.Result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ApiKeyAuthorizeAttributeReturns400WhenApiKeyIsMissing(string value)
        {
            ApiKeyAuthorizeAttribute attribute = CreateAttribute();
            
            // Act
            var result = attribute.CheckForResult(value);

            // Assert
            ResultAssert.IsStatusCode(result, 400, String.Format(Strings.InvalidApiKey, ""));
        }

        [Fact]
        public void ApiKeyAuthorizeAttributeReturns400WhenApiKeyFormatIsInvalid()
        {
            ApiKeyAuthorizeAttribute attribute = CreateAttribute();
            
            // Act
            var result = attribute.CheckForResult("invalid-key");

            // Assert
            ResultAssert.IsStatusCode(result, 400, String.Format(Strings.InvalidApiKey, "invalid-key"));
        }

        [Fact]
        public void ApiKeyAuthorizeAttributeReturns403WhenApiKeyDoesNotBelongToAUser()
        {
            ApiKeyAuthorizeAttribute attribute = CreateAttribute();
            string unknownApiKey = Guid.NewGuid().ToString();

            // Act
            var result = attribute.CheckForResult(unknownApiKey);

            // Assert
            ResultAssert.IsStatusCode(result, 403, String.Format(Strings.ApiKeyNotAuthorized, "push"));
        }

        [Fact]
        public void ApiKeyAuthorizeAttributeReturns403WhenUserIsNotYetConfirmed()
        {
            ApiKeyAuthorizeAttribute attribute = CreateAttribute();
            var user = new User
            {
                UnconfirmedEmailAddress = "unconfirmed@example.com",
                ApiKey = Guid.NewGuid()
            };

            GetMock<IUserService>()
                .Setup(us => us.FindByApiKey(user.ApiKey))
                .Returns(user);

            // Act
            var result = attribute.CheckForResult(user.ApiKey.ToString());

            // Assert
            ResultAssert.IsStatusCode(result, 403, Strings.ApiKeyUserAccountIsUnconfirmed);
        }

        private static Mock<ActionExecutingContext> CreateActionFilterContext(string apiKey)
        {
            var mockFilterContext = new Mock<ActionExecutingContext>();
            var mockController = new Mock<Controller>();

            mockFilterContext.Setup(ctx => ctx.Controller).Returns(mockController.Object);
            mockController.Object.ControllerContext = new ControllerContext
            {
                RouteData = new RouteData
                {
                    Values = { { "apiKey", apiKey } }
                }
            };
            return mockFilterContext;
        }

        private ApiKeyAuthorizeAttribute CreateAttribute()
        {
            ApiKeyAuthorizeAttribute attribute = Get<ApiKeyAuthorizeAttribute>();
            attribute.UserService = Get<IUserService>();
            return attribute;
        }
    }
}