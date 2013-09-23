using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public interface IUserService
    {
        User Create(string username, string password, string emailAddress);

        void UpdateProfile(User user, string emailAddress, bool emailAllowed);

        [Obsolete("Use AuthenticateCredential instead")]
        User FindByApiKey(Guid apiKey);

        User FindByEmailAddress(string emailAddress);

        IList<User> FindByUnconfirmedEmailAddress(string unconfirmedEmailAddress, string optionalUsername);

        User FindByUsername(string username);

        User FindByUsernameAndPassword(string username, string password);

        User FindByUsernameOrEmailAddressAndPassword(string usernameOrEmail, string password);

        [Obsolete("Use ReplaceCredential instead")]
        string GenerateApiKey(string username);

        bool ConfirmEmailAddress(User user, string token);

        bool ChangePassword(string username, string oldPassword, string newPassword);

        User GeneratePasswordResetToken(string usernameOrEmail, int tokenExpirationMinutes);

        bool ResetPasswordWithToken(string username, string token, string newPassword);

        /// <summary>
        /// Gets an authenticated credential, that is it returns a credential IF AND ONLY IF
        /// one exists with exactly the specified type and value.
        /// </summary>
        /// <param name="type">The type of the credential, see <see cref="Constants.CredentialTypes"/></param>
        /// <param name="value">The value of the credential (such as an OAuth ID, API Key, etc.)</param>
        /// <returns>
        /// null if there is no credential matching the request, or a <see cref="Credential"/> 
        /// object WITH the associated <see cref="User"/> object eagerly loaded if there is 
        /// a matching credential
        /// </returns>
        Credential AuthenticateCredential(string type, string value);

        /// <summary>
        /// Creates a new credential for the specified user, overwriting the 
        /// previous credential of the same type, if any. Immediately saves
        /// changes to the database.
        /// </summary>
        /// <param name="userName">The name of the user to create a credential for</param>
        /// <param name="credential">The credential to create</param>
        void ReplaceCredential(string userName, Credential credential);

        /// <summary>
        /// Creates a new credential for the specified user, overwriting the 
        /// previous credential of the same type, if any. Immediately saves
        /// changes to the database.
        /// </summary>
        /// <param name="user">The user object to create a credential for</param>
        /// <param name="credential">The credential to create</param>
        void ReplaceCredential(User user, Credential credential);
    }
}