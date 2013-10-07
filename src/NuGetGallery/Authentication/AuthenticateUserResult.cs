using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Authentication
{
    public class AuthenticateUserResult
    {
        public AuthenticateUserResultStatus Status { get; private set; }

        private AuthenticateUserResult(AuthenticateUserResultStatus status)
        {
            Status = status;
        }

        public static AuthenticateUserResult NoSuchUser()
        {
            return new AuthenticateUserResult(AuthenticateUserResultStatus.NoSuchUser);
        }
    }

    public enum AuthenticateUserResultStatus
    {
        NoSuchUser
    }
}
