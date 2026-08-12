// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGetGallery.Filters;
using Xunit;

namespace NuGetGallery
{
    public class StagingControllerFacts
    {
        [Fact]
        public void RequiresAuthenticationAndStagingScope()
        {
            var attributes = typeof(StagingController).GetCustomAttributes(inherit: true);

            Assert.Contains(attributes, x => x is ApiAuthorizeAttribute);
            var scope = Assert.IsType<ApiScopeRequiredAttribute>(attributes.Single(x => x is ApiScopeRequiredAttribute));
            Assert.Equal(new[] { NuGetScopes.PackageStage }, scope.ScopeActions);
        }

        [Fact]
        public void DisabledFlightHidesTheSurface()
        {
            var user = new User();
            var features = new Mock<IFeatureFlagService>();
            var controller = new TestStagingController(features.Object, user);
            SetApiKeyIdentity(controller, user, user);

            var result = Assert.IsType<HttpStatusCodeResult>(controller.ListStagingGroups());

            Assert.Equal((int)HttpStatusCode.NotFound, result.StatusCode);
        }

        [Fact]
        public void EnabledFlightExposesTheSurface()
        {
            var user = new User { Key = 1 };
            var features = new Mock<IFeatureFlagService>();
            features.Setup(x => x.IsStagingEnabled(user)).Returns(true);
            var staging = new Mock<IStagingService>();
            staging.Setup(x => x.ListGroups(It.Is<StagingAuthorizationContext>(c => c.Owner == user && c.CurrentUser == user))).Returns(new StagingGroupResource[0]);
            var controller = new TestStagingController(features.Object, user, staging.Object);
            SetApiKeyIdentity(controller, user, user);

            var result = Assert.IsType<StagingJsonResult>(controller.ListStagingGroups());

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            staging.Verify(x => x.ListGroups(It.Is<StagingAuthorizationContext>(c => c.Owner == user)), Times.Once);
        }

        [Fact]
        public void EvaluatesFlightForTheCredentialScopeOwner()
        {
            var user = new User { Key = 1 };
            var owner = new User { Key = 2 };
            var features = new Mock<IFeatureFlagService>();
            features.Setup(x => x.IsStagingEnabled(owner)).Returns(true);
            var staging = new Mock<IStagingService>();
            staging.Setup(x => x.ListGroups(It.Is<StagingAuthorizationContext>(c => c.Owner == owner && c.CurrentUser == user))).Returns(new StagingGroupResource[0]);
            var controller = new TestStagingController(features.Object, user, staging.Object);
            SetApiKeyIdentity(controller, user, owner);

            var result = Assert.IsType<StagingJsonResult>(controller.ListStagingGroups());

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            features.Verify(x => x.IsStagingEnabled(owner), Times.Once);
            features.Verify(x => x.IsStagingEnabled(user), Times.Never);
            staging.Verify(x => x.ListGroups(It.Is<StagingAuthorizationContext>(c => c.Owner == owner)), Times.Once);
        }

        [Fact]
        public void RejectsUnscopedCredentialBeforeEvaluatingFlight()
        {
            var user = new User();
            var features = new Mock<IFeatureFlagService>();
            var controller = new TestStagingController(features.Object, user);
            SetApiKeyIdentity(controller, user, owner: null);

            var result = Assert.IsType<StagingJsonResult>(controller.ListStagingGroups());
            var response = Assert.IsType<StagingApiErrorResponse>(result.Value);

            Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
            Assert.Equal(StagingApiErrorCodes.StagingScopeRequired, response.Error.Code);
            features.Verify(x => x.IsStagingEnabled(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task PushStagingPackagePassesMultipartArtifactsAndScopedOwnerToService()
        {
            var user = new User { Key = 1 };
            var owner = new User { Key = 2 };
            var features = new Mock<IFeatureFlagService>();
            features.Setup(x => x.IsStagingEnabled(owner)).Returns(true);
            var staging = new Mock<IStagingService>();
            staging
                .Setup(x => x.UploadAsync(user, owner, It.IsAny<Credential>(), It.Is<StagingUploadRequest>(r =>
                        r.Package != null
                        && r.Symbols == null
                        && r.GroupId == "group"
                        && r.Listed == false)))
                .ReturnsAsync(new StagingUploadResult(new StagedPackageResource
                    {
                        Id = "Package",
                        Version = "1.0.0",
                    },
                    created: true));

            var postedFile = new Mock<HttpPostedFileBase>();
            postedFile.SetupGet(x => x.InputStream).Returns(new MemoryStream(new byte[] { 1 }));
            var files = new Mock<HttpFileCollectionBase>();
            files.SetupGet(x => x.Count).Returns(1);
            files.SetupGet(x => x.AllKeys).Returns(new[] { "package" });
            files.Setup(x => x[0]).Returns(postedFile.Object);
            var request = new Mock<HttpRequestBase>();
            request.SetupGet(x => x.ContentType).Returns("multipart/form-data; boundary=test");
            request.SetupGet(x => x.Files).Returns(files.Object);
            request.SetupGet(x => x.Form).Returns(new NameValueCollection
            {
                ["groupId"] = "group",
                ["listed"] = "false",
            });

            var controller = new TestStagingController(features.Object, user, staging.Object);
            SetApiKeyIdentity(controller, user, owner, request.Object);

            var result = Assert.IsType<StagingJsonResult>(await controller.PushStagingPackage());

            Assert.Equal(HttpStatusCode.Created, result.StatusCode);
            staging.VerifyAll();
        }

        [Fact]
        public async Task PushStagingPackageRejectsNonMultipartRequest()
        {
            var user = new User { Key = 1 };
            var features = new Mock<IFeatureFlagService>();
            features.Setup(x => x.IsStagingEnabled(user)).Returns(true);
            var staging = new Mock<IStagingService>();
            var request = new Mock<HttpRequestBase>();
            request.SetupGet(x => x.ContentType).Returns("application/octet-stream");
            var controller = new TestStagingController(features.Object, user, staging.Object);
            SetApiKeyIdentity(controller, user, user, request.Object);

            var result = Assert.IsType<StagingJsonResult>(await controller.PushStagingPackage());
            var response = Assert.IsType<StagingApiErrorResponse>(result.Value);

            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
            Assert.Equal(StagingApiErrorCodes.InvalidMultipart, response.Error.Code);
            staging.Verify(x => x.UploadAsync(It.IsAny<User>(), It.IsAny<User>(), It.IsAny<Credential>(), It.IsAny<StagingUploadRequest>()), Times.Never);
        }

        [Fact]
        public void GetStagedPackageReturnsServiceResource()
        {
            var user = new User { Key = 1 };
            var features = new Mock<IFeatureFlagService>();
            features.Setup(x => x.IsStagingEnabled(user)).Returns(true);
            var staging = new Mock<IStagingService>();
            staging.Setup(x => x.GetPackage(It.Is<StagingAuthorizationContext>(c => c.Owner == user), "PackageA", "1.0.0")).Returns(new StagedPackageResource { Id = "PackageA", Version = "1.0.0" });
            var controller = new TestStagingController(features.Object, user, staging.Object);
            SetApiKeyIdentity(controller, user, user);

            var result = Assert.IsType<StagingJsonResult>(controller.GetStagedPackage("PackageA", "1.0.0"));

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var stagedPackage = Assert.IsType<StagedPackageResource>(result.Value);
            Assert.Equal("PackageA", stagedPackage.Id);
        }

        [Fact]
        public void GetStagedPackageTranslatesNotFoundException()
        {
            var user = new User { Key = 1 };
            var features = new Mock<IFeatureFlagService>();
            features.Setup(x => x.IsStagingEnabled(user)).Returns(true);
            var staging = new Mock<IStagingService>();
            staging.Setup(x => x.GetPackage(It.Is<StagingAuthorizationContext>(c => c.Owner == user), "PackageA", "1.0.0"))
                .Throws(new StagingApiException(HttpStatusCode.NotFound, StagingApiErrorCodes.StagedPackageNotFound, "The staged package was not found."));
            var controller = new TestStagingController(features.Object, user, staging.Object);
            SetApiKeyIdentity(controller, user, user);

            var result = Assert.IsType<StagingJsonResult>(controller.GetStagedPackage("PackageA", "1.0.0"));
            var response = Assert.IsType<StagingApiErrorResponse>(result.Value);

            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
            Assert.Equal(StagingApiErrorCodes.StagedPackageNotFound, response.Error.Code);
        }

        [Fact]
        public async Task DeleteStagedPackageReturnsNoContentOnSuccess()
        {
            var user = new User { Key = 1 };
            var features = new Mock<IFeatureFlagService>();
            features.Setup(x => x.IsStagingEnabled(user)).Returns(true);
            var staging = new Mock<IStagingService>();
            staging.Setup(x => x.DeletePackageAsync(It.Is<StagingAuthorizationContext>(c => c.Owner == user), "PackageA", "1.0.0")).Returns(Task.CompletedTask);
            var controller = new TestStagingController(features.Object, user, staging.Object);
            SetApiKeyIdentity(controller, user, user);

            var result = Assert.IsType<HttpStatusCodeResult>(await controller.DeleteStagedPackage("PackageA", "1.0.0"));

            Assert.Equal((int)HttpStatusCode.NoContent, result.StatusCode);
            staging.Verify(x => x.DeletePackageAsync(It.Is<StagingAuthorizationContext>(c => c.Owner == user), "PackageA", "1.0.0"), Times.Once);
        }

        [Fact]
        public async Task DownloadStagedPackageStreamsFileThroughGallery()
        {
            var user = new User { Key = 1 };
            var features = new Mock<IFeatureFlagService>();
            features.Setup(x => x.IsStagingEnabled(user)).Returns(true);
            var staging = new Mock<IStagingService>();
            var content = new MemoryStream(new byte[] { 1, 2, 3 });
            staging.Setup(x => x.DownloadPackageAsync(It.Is<StagingAuthorizationContext>(c => c.Owner == user), "PackageA", "1.0.0"))
                .ReturnsAsync(new StagingDownloadResult(content, "PackageA.1.0.0.nupkg", "application/octet-stream"));
            var controller = new TestStagingController(features.Object, user, staging.Object);
            SetApiKeyIdentity(controller, user, user);

            var result = Assert.IsType<FileStreamResult>(await controller.DownloadStagedPackage("PackageA", "1.0.0"));

            Assert.Equal("PackageA.1.0.0.nupkg", result.FileDownloadName);
            Assert.Equal("application/octet-stream", result.ContentType);
        }

        [Fact]
        public async Task CreateStagingGroupPassesBoundRequestAndReturnsCreated()
        {
            var user = new User { Key = 1 };
            var features = new Mock<IFeatureFlagService>();
            features.Setup(x => x.IsStagingEnabled(user)).Returns(true);
            var staging = new Mock<IStagingService>();
            staging.Setup(x => x.CreateGroupAsync(It.Is<StagingAuthorizationContext>(c => c.Owner == user), It.Is<StagingCreateGroupRequest>(r => r.Id == "net10" && r.Name == ".NET 10")))
                .ReturnsAsync(new StagingGroupResult(new StagingGroupResource { Id = "net10", Name = ".NET 10" }, created: true));
            var controller = new TestStagingController(features.Object, user, staging.Object);
            SetApiKeyIdentity(controller, user, user);

            var result = Assert.IsType<StagingJsonResult>(await controller.CreateStagingGroup(new StagingCreateGroupRequest { Id = "net10", Name = ".NET 10" }));

            Assert.Equal(HttpStatusCode.Created, result.StatusCode);
            staging.VerifyAll();
        }

        [Fact]
        public async Task SetStagedPackageListedRejectsAbsentListedField()
        {
            var user = new User { Key = 1 };
            var features = new Mock<IFeatureFlagService>();
            features.Setup(x => x.IsStagingEnabled(user)).Returns(true);
            var staging = new Mock<IStagingService>();
            var controller = new TestStagingController(features.Object, user, staging.Object);
            SetApiKeyIdentity(controller, user, user);

            var result = Assert.IsType<StagingJsonResult>(await controller.SetStagedPackageListed("PackageA", "1.0.0", new StagingListedRequest()));
            var response = Assert.IsType<StagingApiErrorResponse>(result.Value);

            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
            Assert.Equal(StagingApiErrorCodes.InvalidRequestBody, response.Error.Code);
            staging.Verify(x => x.SetListedAsync(It.IsAny<StagingAuthorizationContext>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task SetStagedPackageListedPassesExplicitValueToService()
        {
            var user = new User { Key = 1 };
            var features = new Mock<IFeatureFlagService>();
            features.Setup(x => x.IsStagingEnabled(user)).Returns(true);
            var staging = new Mock<IStagingService>();
            staging.Setup(x => x.SetListedAsync(It.Is<StagingAuthorizationContext>(c => c.Owner == user), "PackageA", "1.0.0", false))
                .ReturnsAsync(new StagedPackageResource { Id = "PackageA", Version = "1.0.0" });
            var controller = new TestStagingController(features.Object, user, staging.Object);
            SetApiKeyIdentity(controller, user, user);

            var result = Assert.IsType<StagingJsonResult>(await controller.SetStagedPackageListed("PackageA", "1.0.0", new StagingListedRequest { Listed = false }));

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            staging.Verify(x => x.SetListedAsync(It.Is<StagingAuthorizationContext>(c => c.Owner == user), "PackageA", "1.0.0", false), Times.Once);
        }

        [Fact]
        public async Task CreateStagingGroupTranslatesConflictException()
        {
            var user = new User { Key = 1 };
            var features = new Mock<IFeatureFlagService>();
            features.Setup(x => x.IsStagingEnabled(user)).Returns(true);
            var staging = new Mock<IStagingService>();
            staging.Setup(x => x.CreateGroupAsync(It.IsAny<StagingAuthorizationContext>(), It.IsAny<StagingCreateGroupRequest>()))
                .ThrowsAsync(new StagingApiException(HttpStatusCode.Conflict, StagingApiErrorCodes.GroupAlreadyExists, "A staging group with this ID already exists with a different name.", "id"));
            var controller = new TestStagingController(features.Object, user, staging.Object);
            SetApiKeyIdentity(controller, user, user);

            var result = Assert.IsType<StagingJsonResult>(await controller.CreateStagingGroup(new StagingCreateGroupRequest { Id = "net10", Name = "Different" }));
            var response = Assert.IsType<StagingApiErrorResponse>(result.Value);

            Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
            Assert.Equal(StagingApiErrorCodes.GroupAlreadyExists, response.Error.Code);
        }

        [Fact]
        public void GroupAlreadyExistsErrorSerializesStableCode()
        {
            var response = new StagingApiErrorResponse(new StagingApiError(StagingApiErrorCodes.GroupAlreadyExists, "A staging group with this ID already exists with a different name.", "id"));

            var json = JsonConvert.SerializeObject(
                response,
                new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore,
                });

            Assert.Contains("\"code\":\"GroupAlreadyExists\"", json);
            Assert.Contains("\"target\":\"id\"", json);
        }

        private static void SetApiKeyIdentity(TestStagingController controller, User user, User owner, HttpRequestBase request = null)
        {
            var scopes = new Scope[0];
            if (owner != null)
            {
                scopes = new[]
                {
                    new Scope(owner, NuGetPackagePattern.AllInclusivePattern, NuGetScopes.PackageStage)
                    {
                        OwnerKey = owner.Key,
                    },
                };
            }

            var credential = new Credential(CredentialTypes.ApiKey.V4, "api-key")
            {
                Scopes = scopes,
            };
            user.Credentials.Add(credential);

            var identity = new ClaimsIdentity(new[] { new Claim(NuGetClaims.ApiKey, credential.Value) }, NuGetGallery.Authentication.AuthenticationTypes.ApiKey);
            var httpContext = new Mock<HttpContextBase>();
            httpContext.SetupGet(x => x.User).Returns(new ClaimsPrincipal(identity));
            httpContext.SetupGet(x => x.Request).Returns(request);
            controller.ControllerContext = new ControllerContext(httpContext.Object, new System.Web.Routing.RouteData(), controller);
        }

        private sealed class TestStagingController : StagingController
        {
            private readonly User _user;

            public TestStagingController(IFeatureFlagService featureFlagService, User user, IStagingService stagingService = null)
                : base(featureFlagService, stagingService ?? Mock.Of<IStagingService>())
            {
                _user = user;
            }

            protected internal override User GetCurrentUser() => _user;
        }
    }
}
