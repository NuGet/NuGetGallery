using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery
{
    public class AuthenticationResult
    {
        public AuthenticationResultStatus Status { get; private set; }
        public User User { get; private set; }
        public IEnumerable<string> Roles { get; private set; }

        private AuthenticationResult(AuthenticationResultStatus status, User user, IEnumerable<string> roles)
        {
            Status = status;
            User = user;
            Roles = Roles;
        }

        public static AuthenticationResult Failure()
        {
            return new AuthenticationResult(
                AuthenticationResultStatus.Failure,
                null,
                Enumerable.Empty<string>());
        }

        public static AuthenticationResult Unconfirmed(User user)
        {
            return new AuthenticationResult(
                AuthenticationResultStatus.Unconfirmed,
                user, 
                Enumerable.Empty<string>());
        }

        public static AuthenticationResult Success(User user, IEnumerable<string> roles)
        {
            return new AuthenticationResult(
                AuthenticationResultStatus.Success,
                user, 
                roles ?? Enumerable.Empty<string>());
        }
    }

    public enum AuthenticationResultStatus
    {
        Success,
        Unconfirmed,
        Failure
    }
}
