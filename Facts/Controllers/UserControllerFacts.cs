using System.Web.Mvc;
using Moq;
using Xunit;

namespace NuGetGallery {
    public class UserControllerFacts {
        public class TheRegisterMethod {
            [Fact]
            public void WillShowTheViewWithErrorsIfTheModelStateIsInvalid() {
                var controller = CreateController();
                controller.ModelState.AddModelError(string.Empty, "aFakeError");

                var result = controller.Register(null) as ViewResult;

                Assert.NotNull(result);
                Assert.Empty(result.ViewName);
            }

            [Fact]
            public void WillCreateTheUser() {
                var userSvc = new Mock<IUserService>();
                userSvc
                    .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(new User() { Username = "theUsername" });
                var controller = CreateController(userSvc: userSvc);

                controller.Register(new RegisterRequest() {
                    Username = "theUsername",
                    Password = "thePassword",
                    EmailAddress = "theEmailAddress",
                });

                userSvc.Verify(x => x.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress"));
            }

            [Fact]
            public void WillSignTheUserIn() {
                var formsAuthSvc = new Mock<IFormsAuthenticationService>();
                var userSvc = new Mock<IUserService>();
                userSvc
                    .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(new User() { Username = "theUsername" });
                var controller = CreateController(
                    formsAuthSvc: formsAuthSvc,
                    userSvc: userSvc);

                controller.Register(new RegisterRequest() {
                    Username = "theUsername",
                    Password = "thePassword",
                    EmailAddress = "theEmailAddress",
                });

                formsAuthSvc.Verify(x => x.SetAuthCookie(
                    "theUsername",
                    true));
            }

            [Fact]
            public void WillInvalidateModelStateAndShowTheViewWhenAnEntityExceptionIsThrow() {
                var userSvc = new Mock<IUserService>();
                userSvc
                    .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Throws(new EntityException("aMessage"));
                var controller = CreateController(userSvc: userSvc);

                var result = controller.Register(new RegisterRequest() {
                    Username = "theUsername",
                    Password = "thePassword",
                    EmailAddress = "theEmailAddress",
                }) as ViewResult;

                Assert.NotNull(result);
                Assert.Empty(result.ViewName);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal("aMessage", controller.ModelState[string.Empty].Errors[0].ErrorMessage);
            }
        }

        static UsersController CreateController(
            Mock<IFormsAuthenticationService> formsAuthSvc = null,
            Mock<IUserService> userSvc = null) {
            formsAuthSvc = formsAuthSvc ?? new Mock<IFormsAuthenticationService>();
            userSvc = userSvc ?? new Mock<IUserService>();
            var packageService = new Mock<IPackageService>();

            return new UsersController(
                formsAuthSvc.Object,
                userSvc.Object,
                packageService.Object);
        }
    }
}
