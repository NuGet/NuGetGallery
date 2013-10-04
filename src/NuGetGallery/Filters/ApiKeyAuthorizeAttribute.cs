using System;
using System.Globalization;
using System.Net;
using System.Web.Mvc;
using Ninject;

namespace NuGetGallery.Filters
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public sealed class ApiKeyAuthorizeAttribute : ActionFilterAttribute
    {
        private IUserService _userService; // for tests

        public IUserService UserService
        {
            get { return _userService ?? Container.Kernel.TryGet<IUserService>(); }
            set { _userService = value; }
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null)
            {
                throw new ArgumentNullException("filterContext");
            }

            var controller = filterContext.Controller;
            string apiKeyStr = (string)((Controller)controller).RouteData.Values["apiKey"];
            filterContext.Result = CheckForResult(apiKeyStr);
        }

        public ActionResult CheckForResult(string apiKeyStr)
        {
            if (String.IsNullOrEmpty(apiKeyStr))
            {
                return new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, String.Format(CultureInfo.CurrentCulture, Strings.InvalidApiKey, apiKeyStr));
            }

            Guid apiKey;
            try
            {
                apiKey = new Guid(apiKeyStr);
            }
            catch
            {
                return new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, String.Format(CultureInfo.CurrentCulture, Strings.InvalidApiKey, apiKeyStr));
            }

            User user = GetUserByApiKey(apiKeyStr);
            if (user == null)
            {
                return new HttpStatusCodeWithBodyResult(HttpStatusCode.Forbidden, String.Format(CultureInfo.CurrentCulture, Strings.ApiKeyNotAuthorized, "push"));
            }

            if (!user.Confirmed)
            {
                return new HttpStatusCodeWithBodyResult(HttpStatusCode.Forbidden, Strings.ApiKeyUserAccountIsUnconfirmed);
            }

            return null;
        }

        // Temporary helper, not necessary after removing the old credential storage
        private User GetUserByApiKey(string apiKey)
        {
            var cred = UserService.AuthenticateCredential(CredentialTypes.ApiKeyV1, apiKey.ToLowerInvariant());
            User user;
            if (cred == null)
            {
#pragma warning disable 0618
                user = UserService.FindByApiKey(Guid.Parse(apiKey));
#pragma warning restore 0618
            }
            else
            {
                user = cred.User;
            }
            return user;
        }
    }
}