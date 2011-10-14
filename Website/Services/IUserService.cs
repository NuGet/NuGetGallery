using System;

namespace NuGetGallery
{
    public interface IUserService
    {
        User Create(
            string username,
            string password,
            string emailAddress);

        string UpdateProfile(User user, string emailAddress, bool emailAllowed);

        User FindByApiKey(Guid apiKey);

        User FindByEmailAddress(string emailAddress);

        User FindByUsername(string username);

        User FindByUsernameAndPassword(
            string username,
            string password);

        string GenerateApiKey(string username);

        bool ConfirmEmailAddress(User user, string token);

        bool ChangePassword(string username, string oldPassword, string newPassword);

        User GeneratePasswordResetToken(string usernameOrEmail, int tokenExpirationMinutes);

        bool ResetPasswordWithToken(string username, string token, string newPassword);
    }
}