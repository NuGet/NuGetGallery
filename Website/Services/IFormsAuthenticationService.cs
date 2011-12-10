
using System.Collections.Generic;
namespace NuGetGallery
{
    public interface IFormsAuthenticationService
    {
        void SetAuthCookie(
            string userName,
            bool createPersistentCookie,
            IEnumerable<string> roles);

        void SignOut();
    }
}