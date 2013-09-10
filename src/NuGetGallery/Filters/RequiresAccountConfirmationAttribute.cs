using System;
using System.Globalization;
using System.Security.Principal;
using System.Web.Mvc;

namespace NuGetGallery
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public class RequiresAccountConfirmationAttribute : ActionFilterAttribute
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
                throw new InvalidOperationException("[RequiresAccountConfirmation] attribute is only valid on [Authenticated] actions.");
            }

            var userService = controller.GetService<IUserService>();
            var currentUser = userService.FindByUsername(user.Identity.Name);
            if (!currentUser.Confirmed)
            {
                controller.TempData["ConfirmationRequiredMessage"] = String.Format(
                    CultureInfo.CurrentCulture,
                    "Before you can {0} you must first confirm your email address.", _inOrderTo);
                controller.HttpContext.SetConfirmationContext(_inOrderTo, controller.Url.Current());
                filterContext.Result = new RedirectResult(controller.Url.ConfirmationRequired());
            }
        }
    }
}