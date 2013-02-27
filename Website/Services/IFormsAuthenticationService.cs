using System.Collections.Generic;
using System.Web;

namespace NuGetGallery
{
    public interface IFormsAuthenticationService
    {
        void SetAuthCookie(
            string userName,
            bool createPersistentCookie,
            IEnumerable<string> roles);

        void SignOut();

        bool ShouldForceSSL(HttpContextBase context);
    }
}