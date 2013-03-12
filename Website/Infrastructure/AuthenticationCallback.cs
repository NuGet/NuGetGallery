using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.WebPages;
using System.Web.Routing;
using WorldDomination.Web.Authentication;
using WorldDomination.Web.Authentication.Mvc;

namespace NuGetGallery.Infrastructure
{
    public class AuthenticationCallback : IAuthenticationCallbackProvider
    {
        public IUserService UserService { get; protected set; }
        public IFormsAuthenticationService FormsAuth { get; protected set; }
        public ICryptographyService Crypto { get; protected set; }

        protected AuthenticationCallback() { }

        public AuthenticationCallback(IUserService userService, IFormsAuthenticationService formsAuth, ICryptographyService crypto) : this()
        {
            if (userService == null)
            {
                throw new ArgumentNullException("userService");
            }

            if (formsAuth == null)
            {
                throw new ArgumentNullException("formsAuth");
            }

            if (crypto == null)
            {
                throw new ArgumentNullException("crypto");
            }

            UserService = userService;
            FormsAuth = formsAuth;
            Crypto = crypto;
        }

        public ActionResult Process(HttpContextBase context, AuthenticateCallbackData model)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (model == null)
            {
                throw new ArgumentNullException("model");
            }

            if (model.Exception != null)
            {
                throw model.Exception;
            }

            if (model.AuthenticatedClient == null || model.AuthenticatedClient.UserInformation == null)
            {
                throw new ArgumentException("Didn't get any authentication or user data from the OAuth provider?", "model");
            }
            var providerUserInfo = model.AuthenticatedClient.UserInformation;

            if (String.IsNullOrEmpty(providerUserInfo.Id))
            {
                throw new AuthenticationException("Didn't get a user ID from the OAuth provider?");
            }

            // Look up a user with this credential
            var user = UserService.FindByCredential("oauth:" + model.AuthenticatedClient.ProviderName, providerUserInfo.Id);
            
            if (user != null)
            {
                if (user.Confirmed)
                {
                    return LogInUser(user, model.RedirectUrl);
                }
                else
                {
                    return new RedirectToRouteResult(
                        MVC.Users.Thanks().GetRouteValueDictionary());
                }
            }
            else
            {
                // Construct a token and go to the Link/Create User page
                return new RedirectToRouteResult(
                    MVC.Authentication.LinkOrCreateUser(
                        CalculateToken(
                            providerUserInfo.Email,
                            providerUserInfo.UserName,
                            providerUserInfo.Id,
                            model.AuthenticatedClient.ProviderName),
                        (model.RedirectUrl == null || model.RedirectUrl.IsAbsoluteUri) ? null : model.RedirectUrl.OriginalString)
                    .GetRouteValueDictionary());
            }
        }

        private ActionResult LogInUser(User user, Uri returnUrl)
        {
            FormsAuth.SetAuthCookie(
                user,
                true);

            // Safe redirect
            return SafeRedirect(returnUrl);
        }

        protected virtual string CalculateToken(string email, string userName, string id, string providerName)
        {
            return Crypto.EncryptString(
                OAuthLinkToken.CalculateToken(
                    email, userName, id, providerName),
                OAuthLinkToken.CryptoPurpose);
        }

        private ActionResult SafeRedirect(Uri returnUrl)
        {
            if (returnUrl != null && !returnUrl.IsAbsoluteUri)
            {
                return new RedirectResult(returnUrl.OriginalString);
            }
            return new RedirectToRouteResult(MVC.Pages.Home().GetRouteValueDictionary());
        }
    }
}
