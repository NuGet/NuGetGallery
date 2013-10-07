using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Authentication
{
    public class AuthenticateUserResult
    {
        public User User { get; private set; }
        public IEnumerable<string> Roles { get; private set; }
    }
}
