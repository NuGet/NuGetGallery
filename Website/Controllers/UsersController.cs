using System.Web.Mvc;

namespace NuGetGallery
{
    public class UsersController : Controller
    {
        public const string Name = "Users";

        readonly IFormsAuthenticationService formsAuthSvc;
        readonly IUsersService userService;
        
        public UsersController(
            IFormsAuthenticationService formsAuthSvc,
            IUsersService userSvc)
        {
            this.formsAuthSvc = formsAuthSvc;
            this.userService = userSvc;
        }

        [ActionName(ActionName.Register)]
        public ActionResult ShowRegisterForm()
        {
            return View();
        }

        [ActionName(ActionName.Register), HttpPost, ValidateAntiForgeryToken]
        public ActionResult Register(RegisterRequest request)
        {
            // TODO: consider client-side validation for unique username
            // TODO: add email validation
            
            if (!ModelState.IsValid)
                return View();

            User user;
            try
            {
                user = userService.Create(
                    request.Username,
                    request.Password,
                    request.EmailAddress);
            }
            catch (EntityException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View();
            }

            formsAuthSvc.SetAuthCookie(
                user.Username,
                true);
            
            return RedirectToRoute(RouteName.Home);
        }
    }
}
