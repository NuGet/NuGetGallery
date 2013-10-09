using System;
using System.Globalization;
using System.Security.Principal;
using System.Web.Mvc;

namespace NuGetGallery.Filters
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public sealed class RequiresAccountConfirmationAttribute : ActionFilterAttribute
    {
        private string _inOrderTo;

        public RequiresAccountConfirmationAttribute(string inOrderTo)
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
            IPrincipal user = controller.User;
            if (!user.Identity.IsAuthenticated)
            {
                throw new InvalidOperationException("Requires account confirmation attribute is only valid on authenticated actions.");
            }

            var userService = controller.GetService<IUserService>();
            var currentUser = userService.FindByUsername(user.Identity.Name);
            if (!currentUser.Confirmed)
            {
                controller.TempData["ConfirmationRequiredMessage"] = String.Format(
                    CultureInfo.CurrentCulture,
                    "Before you can {0} you must first confirm your email address.", _inOrderTo);
                controller.HttpContext.SetConfirmationReturnUrl(controller.Url.Current());
                filterContext.Result = new RedirectResult(controller.Url.ConfirmationRequired());
            }
        }
    }
}