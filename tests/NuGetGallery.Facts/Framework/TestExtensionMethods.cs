using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public static class TestExtensionMethods
    {
        public static void SetUser(this AppController self, string userName)
        {
            SetUser(self, new User(userName));
        }

        public static void SetUser(this AppController self, User user)
        {
            Mock.Get(self.HttpContext).Setup(c => c.Request.IsAuthenticated).Returns(true);
            Mock.Get(self.HttpContext).Setup(c => c.User).Returns(
                new ClaimsPrincipal(
                    AuthenticationService.CreateIdentity(user, "Test")));
        }
    }
}
