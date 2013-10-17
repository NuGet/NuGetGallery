using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public static class TestExtensionMethods
    {
        /// <summary>
        /// Should only be used in the rare cases where you are testing an action that
        /// does NOT use AppController.GetCurrentUser()! In those cases, use 
        /// AppController.SetCurrentUser instead.
        /// </summary>
        /// <param name="name"></param>
        public static void SetPrincipal(this AppController self, string name)
        {
            var mock = Mock.Get(self.HttpContext);
            mock.Setup(c => c.Request.IsAuthenticated).Returns(true);
            mock.Setup(c => c.User).Returns(new ClaimsPrincipal(
                new ClaimsIdentity(
                    new [] { new Claim(ClaimTypes.Name, name) })));
        }
    }
}
