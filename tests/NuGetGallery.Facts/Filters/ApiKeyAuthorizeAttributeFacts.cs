using System;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Framework;
using Xunit;
using Xunit.Extensions;
using System.Collections.Generic;

namespace NuGetGallery.Filters
{
    public class ApiKeyAuthorizeAttributeFacts : TestContainer
    {
        [Fact]
        public void ApiKeyAuthorizeAttributeAcceptsValidRequestsFromConfirmedPackageOwners()
        {
            ApiKeyAuthorizeAttribute attribute = Get<ApiKeyAuthorizeAttribute>();
            attribute.UserService = Get<IUserService>();
            var mockFilterContext = new Mock<ActionExecutingContext>();
            var mockController = new Mock<Controller>();

            var key = Guid.NewGuid().ToString();

            mockFilterContext.Setup(ctx => ctx.Controller).Returns(mockController.Object);
            mockFilterContext.Setup(ctx => ctx.ActionParameters).Returns(
                new Dictionary<string, object>
                {
                    { "apiKey", key }
                });

            GetMock<IUserService>()
                .Setup(us => us.FindByApiKey(It.IsAny<Guid>()))
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
            ApiKeyAuthorizeAttribute attribute = Get<ApiKeyAuthorizeAttribute>();
            attribute.UserService = Get<IUserService>();

            // Act
            var result = attribute.CheckForResult(value);

            // Assert
            ResultAssert.IsStatusCode(result, 400, String.Format(Strings.InvalidApiKey, ""));
        }

        [Fact]
        public void ApiKeyAuthorizeAttributeReturns400WhenApiKeyFormatIsInvalid()
        {
            ApiKeyAuthorizeAttribute attribute = Get<ApiKeyAuthorizeAttribute>();
            attribute.UserService = Get<IUserService>();

            // Act
            var result = attribute.CheckForResult("invalid-key");

            // Assert
            ResultAssert.IsStatusCode(result, 400, String.Format(Strings.InvalidApiKey, "invalid-key"));
        }

        [Fact]
        public void ApiKeyAuthorizeAttributeReturns403WhenApiKeyDoesNotBelongToAUser()
        {
            ApiKeyAuthorizeAttribute attribute = Get<ApiKeyAuthorizeAttribute>();
            attribute.UserService = Get<IUserService>();
            string unknownApiKey = Guid.NewGuid().ToString();

            // Act
            var result = attribute.CheckForResult(unknownApiKey);

            // Assert
            ResultAssert.IsStatusCode(result, 403, String.Format(Strings.ApiKeyNotAuthorized, "push"));
        }

        [Fact]
        public void ApiKeyAuthorizeAttributeReturns403WhenUserIsNotYetConfirmed()
        {
            ApiKeyAuthorizeAttribute attribute = Get<ApiKeyAuthorizeAttribute>();
            attribute.UserService = Get<IUserService>();
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
    }
}