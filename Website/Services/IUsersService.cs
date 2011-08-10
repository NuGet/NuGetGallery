
namespace NuGetGallery {
    public interface IUsersService {
        User Create(
            string username,
            string password,
            string emailAddress);

        User FindByEmailAddress(string emailAddress);

        User FindByUsername(string username);

        User FindByUsernameAndPassword(
            string username,
            string password);
    }
}