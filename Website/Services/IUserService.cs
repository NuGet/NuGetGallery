using System;
using System.Collections.Generic;
using System.Linq;

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

        string GenerateApiKey(string username);

        bool ConfirmEmailAddress(User user, string token);

        bool ChangePassword(string username, string oldPassword, string newPassword);

        User GeneratePasswordResetToken(string usernameOrEmail, int tokenExpirationMinutes);

        bool ResetPasswordWithToken(string username, string token, string newPassword);

        void Follow(string username, string packageId, bool saveChanges);

        void Unfollow(string username, string packageId, bool saveChanges);

        bool IsFollowing(string username, string packageId);

        IEnumerable<string> GetFollowedPackageIdsInSet(string username, string[] packageIds);

        IQueryable<UserFollowsPackage> GetFollowedPackages(User user);
    }
}