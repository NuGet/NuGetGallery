using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public interface IUserService
    {
        User Create(string username, string password, string emailAddress);

        void UpdateProfile(User user, bool emailAllowed);

        User FindByApiKey(Guid apiKey);

        User FindByEmailAddress(string emailAddress);

        IList<User> FindByUnconfirmedEmailAddress(string unconfirmedEmailAddress, string optionalUsername);

        User FindByUsername(string username);

        User FindByUsernameAndPassword(string username, string password);

        User FindByUsernameOrEmailAddressAndPassword(string usernameOrEmail, string password);

        string GenerateApiKey(string username);

        bool ConfirmEmailAddress(User user, string token);

        bool ChangeEmailAddress(User user, string newEmailAddress);

        bool ChangePassword(string username, string oldPassword, string newPassword);

        User GeneratePasswordResetToken(string usernameOrEmail, int tokenExpirationMinutes);

        bool ResetPasswordWithToken(string username, string token, string newPassword);
    }

    public static class IUserServiceExtensions
    {
        public static bool ChangeEmailAddress(this IUserService service, string username, string password, string newEmailAddress)
        {
            var user = service.FindByUsernameAndPassword(username, password);
            return 
                user != null &&
                service.ChangeEmailAddress(user, newEmailAddress);
        }
    }
}