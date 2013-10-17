using System;
using System.Security.Claims;
using System.Security.Principal;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Microsoft.Owin;
using NuGetGallery.Authentication;
using System.Net;

namespace NuGetGallery
{
    public abstract partial class AppController : Controller
    {
        private IOwinContext _overrideContext;
        
        public IOwinContext OwinContext
        {
            get { return _overrideContext ?? HttpContext.GetOwinContext(); }
            set { _overrideContext = value; }
        }

        public new ClaimsPrincipal User
        {
            get { return base.User as ClaimsPrincipal; }
        }

        protected internal virtual T GetService<T>()
        {
            return DependencyResolver.Current.GetService<T>();
        }

        // This is a method because the first call will perform a database call
        /// <summary>
        /// Get the current user, from the database, or if someone in this request has already
        /// retrieved it, from memory. This will NEVER return null. It will throw an exception
        /// that will yield an HTTP 401 if it would return null. As a result, it should only
        /// be called in actions with the Authorize attribute or a Request.IsAuthenticated check
        /// </summary>
        /// <returns>The current user</returns>
        protected internal User GetCurrentUser()
        {
            User user = null;
            object obj;
            if (OwinContext.Environment.TryGetValue(Constants.CurrentUserOwinEnvironmentKey, out obj))
            {
                user = obj as User;
            }

            if (user == null)
            {
                user = LoadUser();
                OwinContext.Environment[Constants.CurrentUserOwinEnvironmentKey] = user;
            }

            if (user == null)
            {
                // Unauthorized! If we get here it's because a valid session token was presented, but the
                // user doesn't exist any more. So we just have a generic error.
                throw new HttpException(401, Strings.Unauthorized);
            }

            return user;
        }

        protected internal void SetCurrentUser(User user)
        {
            OwinContext.Environment[Constants.CurrentUserOwinEnvironmentKey] = user;
        }

        private User LoadUser()
        {
            var principal = OwinContext.Authentication.User;
            if(principal != null)
            {
                // Try to authenticate with the user name
                string userName = principal.GetClaimOrDefault(ClaimTypes.Name);
                
                if (!String.IsNullOrEmpty(userName))
                {
                    return DependencyResolver
                        .Current
                        .GetService<UserService>()
                        .FindByUsername(userName);
                }
            }
            return null; // No user logged in, or credentials could not be resolved
        }
    }
}