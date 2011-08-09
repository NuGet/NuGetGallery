using System.Web.Mvc;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class UserControllerFacts
    {
        public class The_Register_method
        {
            [Fact]
            public void will_show_the_view_with_errors_if_the_model_state_is_invalid()
            {
                var controller = CreateController();
                controller.ModelState.AddModelError(string.Empty, "aFakeError");

                var result = controller.Register(null) as ViewResult;

                Assert.NotNull(result);
                Assert.Empty(result.ViewName);
            }

            [Fact]
            public void will_create_the_user()
            {
                var usersSvc = new Mock<IUsersService>();
                usersSvc
                    .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(new User() { Username = "theUsername" });
                var controller = CreateController(usersSvc: usersSvc);

                controller.Register(new RegisterRequest()
                    {
                        Username = "theUsername",
                        Password = "thePassword",
                        EmailAddress = "theEmailAddress",
                    });

                usersSvc.Verify(x => x.Create(
                    "theUsername", 
                    "thePassword", 
                    "theEmailAddress"));
            }

            [Fact]
            public void will_sign_the_user_in()
            {
                var formsAuthSvc = new Mock<IFormsAuthenticationService>();
                var usersSvc = new Mock<IUsersService>();
                usersSvc
                    .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(new User() { Username = "theUsername" });
                var controller = CreateController(
                    formsAuthSvc: formsAuthSvc,
                    usersSvc: usersSvc);

                controller.Register(new RegisterRequest()
                {
                    Username = "theUsername",
                    Password = "thePassword",
                    EmailAddress = "theEmailAddress",
                });

                formsAuthSvc.Verify(x => x.SetAuthCookie(
                    "theUsername",
                    true));
            }

            [Fact]
            public void will_invalidate_model_state_and_show_the_view_with_errors_when_a_domain_exception_is_throw()
            {
                var usersSvc = new Mock<IUsersService>();
                usersSvc
                    .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Throws(new EntityException("aMessage"));
                var controller = CreateController(usersSvc: usersSvc);

                var result = controller.Register(new RegisterRequest()
                {
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
            Mock<IUsersService> usersSvc = null)
        {
            formsAuthSvc = formsAuthSvc ?? new Mock<IFormsAuthenticationService>();
            usersSvc = usersSvc ?? new Mock<IUsersService>();

            return new UsersController(
                formsAuthSvc.Object,
                usersSvc.Object);
        }
    }
}
