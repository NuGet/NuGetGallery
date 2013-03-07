using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using WorldDomination.Web.Authentication;
using WorldDomination.Web.Authentication.Mvc;

namespace NuGetGallery.Infrastructure
{
    public class AuthenticationCallback : IAuthenticationCallbackProvider
    {
        private readonly IUserService _userService;

        public AuthenticationCallback(IUserService userService)
        {
            _userService = userService;
        }

        public ActionResult Process(HttpContextBase context, AuthenticateCallbackData model)
        {
            if (model.Exception != null)
            {
                throw model.Exception;
            }

            if (model.AuthenticatedClient == null || model.AuthenticatedClient.UserInformation == null)
            {
                throw new InvalidOperationException("Didn't get any authentication or user data from the OAuth provider?");
            }
            var providerUserInfo = model.AuthenticatedClient.UserInformation;

            if (String.IsNullOrEmpty(providerUserInfo.Id))
            {
                throw new InvalidOperationException("Didn't get a user ID from the OAuth provider?");
            }

            // Look up a user with this credential
            var user = _userService.FindByCredential("oauth:" + model.AuthenticatedClient.ProviderName, providerUserInfo.Id);

            if (user != null)
            {
                return LogInUser(user);
            }
            else
            {
                return LinkOrCreate(providerUserInfo);
            }
        }

        private ActionResult LogInUser(User user)
        {
            throw new NotImplementedException();
        }

        private ActionResult LinkOrCreate(UserInformation providerUserInfo)
        {
            throw new NotImplementedException();
        }
    }
}
