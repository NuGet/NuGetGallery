using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IUserService
    {
        void ChangeEmailSubscription(User user, bool emailAllowed);

        User FindByEmailAddress(string emailAddress);

        IList<User> FindAllByEmailAddress(string emailAddress);

        IList<User> FindByUnconfirmedEmailAddress(string unconfirmedEmailAddress, string optionalUsername);

        User FindByUsername(string username);

        Task<bool> ConfirmEmailAddress(User user, string token);

        Task ChangeEmailAddress(User user, string newEmailAddress);

        Task CancelChangeEmailAddress(User user);
    }
}