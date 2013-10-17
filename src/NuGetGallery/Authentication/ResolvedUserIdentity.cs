using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace NuGetGallery.Authentication
{
    public class ResolvedUserIdentity : ClaimsIdentity
    {
        public User User { get; private set; }

        public ResolvedUserIdentity(AuthenticatedUser authUser, string authenticationType)
            : base(authenticationType)
        {
            User = authUser.User;

            AddClaim(new Claim(ClaimTypes.Name, User.Username));
            AddClaim(new Claim(ClaimTypes.Email, User.EmailAddress));

            if (User.Roles != null)
            {
                AddClaims(User.Roles.Select(r => new Claim(ClaimTypes.Role, r.Name)));
            }
        }
    }
}
