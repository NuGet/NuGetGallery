using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Threading;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery.Filters
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public class ApiKeyAuthorizeAttribute : ActionFilterAttribute
    {
        private string _inOrderTo;

        public ApiKeyAuthorizeAttribute(string inOrderTo)
        {
            _inOrderTo = inOrderTo;
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null)
            {
                throw new ArgumentNullException("filterContext");
            }

            var controller = ((AppController)filterContext.Controller);
            Guid apiKey = default(Guid);
            try
            {
                apiKey = new Guid((string)controller.RouteData.Values["apiKey"]);
            }
            catch
            {
                filterContext.Result = new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, Strings.InvalidApiKey);
                return;
            }

            var userService = controller.GetService<UserService>();
            if (userService == null)
            {
                throw new InvalidOperationException("The controller must have a UserService to use [AuthorizeWithApiKeyAttribute] attribute.");
            }

            User user = userService.FindByApiKey(apiKey);
            if (user == null)
            {
                filterContext.Result = new HttpStatusCodeWithBodyResult(HttpStatusCode.Forbidden, Strings.ApiKeyNotAuthorized);
                return;
            }

            if (!user.Confirmed)
            {
                filterContext.Result = new HttpStatusCodeWithBodyResult(HttpStatusCode.Forbidden, Strings.ApiKeyUserAccountIsUnconfirmed);
                return;
            }

            var principal = new GenericPrincipal(new GenericIdentity(user.Username), new string[0]);
            filterContext.HttpContext.User = Thread.CurrentPrincipal = principal;
        }
    }
}