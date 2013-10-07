using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Authentication
{
    public class AuthenticateUserResult
    {
        public static readonly AuthenticateUserResult Failed = new AuthenticateUserResult();

        public bool Success { get; private set; }
        public User User { get; private set; }

        private AuthenticateUserResult()
        {
            Success = false;
            User = null;
        }

        public AuthenticateUserResult(User user)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            
            Success = true;
            User = user;
        }
    }
}
