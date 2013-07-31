using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class AuthenticationService
    {
        public IUserService Users { get; protected set; }
        
        // For sub-classes to initialize services themselves
        protected AuthenticationService()
        {
        }

        public AuthenticationService(
            IFormsAuthenticationService formsAuthService,
            IUserService userService)
        {
            Users = userService;
        }

        public virtual AuthenticationResult Authenticate(string userNameOrEmail, string password)
        {
            var user = Users.FindByUsernameOrEmailAddressAndPassword(
                userNameOrEmail,
                password);

            if (user == null)
            {
                return AuthenticationResult.Failure();
            }

            if (!user.Confirmed)
            {
                return AuthenticationResult.Unconfirmed(user);
            }

            IEnumerable<string> roles = null;
            if (user.Roles.AnySafe())
            {
                roles = user.Roles.Select(r => r.Name);
            }
            return AuthenticationResult.Success(user, roles);
        }
    }
}