using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;

namespace NuGetGallery.Framework
{
    public static class TestExtensionMethods
    {
        public static void SetUser(this Controller self, User user)
        {
            Mock.Get(self.HttpContext).Setup(c => c.User).Returns(user.ToPrincipal());
        }
    }
}

