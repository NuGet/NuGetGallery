using System;

namespace NuGetGallery
{
    public interface IUserService
    {
        User Create(string username, string password, string emailAddress);

        void UpdateProfile(User user, string emailAddress, bool emailAllowed);

        User FindByApiKey(Guid apiKey);

        User FindByEmailAddress(string emailAddress);

        User FindByUnconfirmedEmailAddress(string unconfirmedEmailAddress);

        User FindByUsername(string username);

        User FindByUsernameAndPassword(string username, string password);

        User FindByUsernameOrEmailAddressAndPassword(string usernameOrEmail, string password);

        void Follow(User user, PackageRegistration package, bool saveChanges);

        string GenerateApiKey(string username);

        bool ConfirmEmailAddress(User user, string token);

        bool ChangePassword(string username, string oldPassword, string newPassword);

        User GeneratePasswordResetToken(string usernameOrEmail, int tokenExpirationMinutes);

        bool ResetPasswordWithToken(string username, string token, string newPassword);

        void Unfollow(User user, PackageRegistration package, bool saveChanges);

        bool IsFollowing(User user, PackageRegistration package);
    }
}