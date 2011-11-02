using System.Security.Principal;
using System.Web;
using Moq;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class JsonApiControllerFacts
    {
        public class TheAddPackageOwnerMethod
        {
            [Fact]
            public void ReturnsFailureWhenPackageNotFound()
            {
                var controller = CreateJsonApiController();

                var result = controller.AddPackageOwner("foo", "steve");

                Assert.False(TestUtility.GetAnonymousPropertyValue<bool>(result, "success"));
                Assert.Equal("Package not found", TestUtility.GetAnonymousPropertyValue<string>(result, "message"));
            }

            [Fact]
            public void DoesNotAllowNonPackageOwnerToAddPackageOwner()
            {
                var packageService = new Mock<IPackageService>();
                packageService.Setup(svc => svc.FindPackageRegistrationById("foo")).Returns(new PackageRegistration());
                var controller = CreateJsonApiController(packageService);

                var result = controller.AddPackageOwner("foo", "steve");

                Assert.False(TestUtility.GetAnonymousPropertyValue<bool>(result, "success"));
                Assert.Equal("You are not the package owner.", TestUtility.GetAnonymousPropertyValue<string>(result, "message"));
            }

            [Fact]
            public void ReturnsFailureWhenRequestedNewOwnerDoesNotExist()
            {
                var package = new PackageRegistration { Id = "foo", Owners = new[] { new User { Username = "scott" } } };
                var packageService = new Mock<IPackageService>();
                packageService.Setup(svc => svc.FindPackageRegistrationById("foo")).Returns(package);
                var currentUser = new Mock<IPrincipal>();
                currentUser.Setup(u => u.Identity.Name).Returns("scott");
                var controller = CreateJsonApiController(packageService, currentUser: currentUser);

                var result = controller.AddPackageOwner("foo", "steve");

                Assert.False(TestUtility.GetAnonymousPropertyValue<bool>(result, "success"));
                Assert.Equal("Owner not found", TestUtility.GetAnonymousPropertyValue<string>(result, "message"));
            }

            [Fact]
            public void CreatesPackageOwnerRequestSendsEmailAndReturnsPendingState()
            {
                var newOwner = new User { Username = "steve" };
                var currentOwner = new User { Username = "scott" };
                var package = new PackageRegistration { Id = "foo", Owners = new[] { currentOwner } };
                var packageOwnerRequest = new PackageOwnerRequest { ConfirmationCode = "some-generated-code" };
                var currentUser = new Mock<IPrincipal>();
                currentUser.Setup(u => u.Identity.Name).Returns("scott");
                var userService = new Mock<IUserService>();
                userService.Setup(u => u.FindByUsername(currentOwner.Username)).Returns(currentOwner);
                userService.Setup(u => u.FindByUsername(newOwner.Username)).Returns(newOwner);
                var packageService = new Mock<IPackageService>();
                packageService.Setup(svc => svc.FindPackageRegistrationById("foo")).Returns(package);
                packageService.Setup(svc => svc.CreatePackageOwnerRequest(package, currentOwner, It.IsAny<User>())).Returns(packageOwnerRequest);
                var messageService = new Mock<IMessageService>();
                messageService.Setup(m => m.SendPackageOwnerRequest(
                    currentOwner,
                    newOwner,
                    package,
                    "https://example.org/?Controller=Packages&Action=ConfirmOwner&id=foo&username=steve&token=some-generated-code")).Verifiable();
                var controller = CreateJsonApiController(packageService, userService, currentUser: currentUser, messageService: messageService);

                var result = controller.AddPackageOwner("foo", newOwner.Username);

                // We use a catch-all route for unit tests so we can see the parameters 
                // are passed correctly.
                Assert.True(TestUtility.GetAnonymousPropertyValue<bool>(result, "success"));
                Assert.Equal(newOwner.Username, TestUtility.GetAnonymousPropertyValue<string>(result, "name"));
                Assert.True(TestUtility.GetAnonymousPropertyValue<bool>(result, "pending"));
                messageService.VerifyAll();
            }
        }

        static JsonApiController CreateJsonApiController(
            Mock<IPackageService> packageService = null,
            Mock<IUserService> userService = null,
            Mock<IEntityRepository<PackageOwnerRequest>> repository = null,
            Mock<IMessageService> messageService = null,
            Mock<IPrincipal> currentUser = null)
        {
            packageService = packageService ?? new Mock<IPackageService>();
            userService = userService ?? new Mock<IUserService>();
            repository = repository ?? new Mock<IEntityRepository<PackageOwnerRequest>>();
            messageService = messageService ?? new Mock<IMessageService>();
            currentUser = currentUser ?? new Mock<IPrincipal>();

            var httpContext = new Mock<HttpContextBase>();
            httpContext.Setup(c => c.User).Returns(currentUser.Object);
            var controller = new JsonApiController(packageService.Object, userService.Object, repository.Object, messageService.Object);
            TestUtility.SetupHttpContextMockForUrlGeneration(httpContext, controller);
            return controller;
        }
    }
}
