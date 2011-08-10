
namespace NuGetGallery {
    public interface IFormsAuthenticationService {
        void SetAuthCookie(
            string userName,
            bool createPersistentCookie);

        void SignOut();
    }
}