using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public interface IUserService
    {
        void ChangeEmailSubscription(User user, bool emailAllowed);

        User FindByEmailAddress(string emailAddress);

        IList<User> FindAllByEmailAddress(string emailAddress);

        IList<User> FindByUnconfirmedEmailAddress(string unconfirmedEmailAddress, string optionalUsername);

        User FindByUsername(string username);

        bool ConfirmEmailAddress(User user, string token);

        void ChangeEmailAddress(User user, string newEmailAddress);
    }
}