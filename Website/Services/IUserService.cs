using System;

namespace NuGetGallery {
    public interface IUserService {
        User Create(
            string username,
            string password,
            string emailAddress);

        User FindByApiKey(Guid apiKey);

        User FindByEmailAddress(string emailAddress);

        User FindByUsername(string username);

        User FindByUsernameAndPassword(
            string username,
            string password);

        string GenerateApiKey(string username);

        bool ConfirmAccount(string token);

        bool ChangePassword(string username, string oldPassword, string newPassword);

        User GeneratePasswordResetToken(string usernameOrEmail, int tokenExpirationMinutes);

        bool ResetPasswordWithToken(string username, string token, string newPassword);
    }
}