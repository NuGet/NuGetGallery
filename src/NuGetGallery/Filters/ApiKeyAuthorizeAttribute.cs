using System;
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
                return new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, Strings.InvalidApiKey);
            }

            Guid apiKey;
            try
            {
                apiKey = new Guid(apiKeyStr);
            }
            catch
            {
                return new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, Strings.InvalidApiKey);
            }

            User user = UserService.FindByApiKey(apiKey);
            if (user == null)
            {
                return new HttpStatusCodeWithBodyResult(HttpStatusCode.Forbidden, Strings.ApiKeyNotAuthorized);
            }

            if (!user.Confirmed)
            {
                return new HttpStatusCodeWithBodyResult(HttpStatusCode.Forbidden, Strings.ApiKeyUserAccountIsUnconfirmed);
            }

            return null;
        }
    }
}