using System.Collections.Generic;
using System.Web;

namespace NuGetGallery
{
    public interface IFormsAuthenticationService
    {
        void SetAuthCookie(
            User user,
            bool createPersistentCookie);

        void SignOut();

        bool ShouldForceSSL(HttpContextBase context);
    }
}