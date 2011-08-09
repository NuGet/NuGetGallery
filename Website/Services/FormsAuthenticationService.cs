using System.Web.Security;

namespace NuGetGallery
{
    public class FormsAuthenticationService : IFormsAuthenticationService
    {
        public void SetAuthCookie(
            string userName, 
            bool createPersistentCookie)
        {
            FormsAuthentication.SetAuthCookie(
                userName,
                createPersistentCookie);
        }


        public void SignOut()
        {
            FormsAuthentication.SignOut();
        }
    }
}