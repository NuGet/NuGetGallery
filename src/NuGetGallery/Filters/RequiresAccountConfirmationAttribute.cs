using System;
using System.Globalization;
using System.Security.Principal;
using System.Web.Mvc;
using NuGetGallery.Authentication;

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

            if (!filterContext.HttpContext.Request.IsAuthenticated)
            {
                throw new InvalidOperationException("Requires account confirmation attribute is only valid on authenticated actions.");
            }
            
            var controller = ((AppController)filterContext.Controller);
            var user = controller.GetCurrentUser();
            
            if (!user.Confirmed)
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