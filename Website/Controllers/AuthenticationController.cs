using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Net.Mail;
using System.Web.Mvc;

namespace NuGetGallery
{
    public partial class AuthenticationController : Controller
    {
        public IFormsAuthenticationService FormsAuth { get; protected set; }
        public IUserService Users { get; protected set; }
        public ICryptographyService Crypto { get; protected set; }
        public IConfiguration Config { get; protected set; }
        public IMessageService Messages { get; protected set; }

        // For sub-classes to initialize services themselves
        protected AuthenticationController()
        {
        }

        public AuthenticationController(
            IFormsAuthenticationService formsAuthService,
            IUserService userService,
            ICryptographyService cryptoService,
            IConfiguration config,
            IMessageService messages)
        {
            FormsAuth = formsAuthService;
            Users = userService;
            Crypto = cryptoService;
            Config = config;
            Messages = messages;
        }

        [RequireRemoteHttps(OnlyWhenAuthenticated = false)]
        public virtual ActionResult LogOn(string returnUrl)
        {
            // I think it should be obvious why we don't want the current URL to be the return URL here ;)
            ViewData[Constants.ReturnUrlViewDataKey] = returnUrl;
            return View(new SignInRequest() { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [RequireRemoteHttps(OnlyWhenAuthenticated = false)]
        public virtual ActionResult LogOn(SignInRequest request, string returnUrl)
        {
            // I think it should be obvious why we don't want the current URL to be the return URL here ;)
            ViewData[Constants.ReturnUrlViewDataKey] = returnUrl;

            // TODO: improve the styling of the validation summary
            // TODO: modify the Object.cshtml partial to make the first text box autofocus, or use additional metadata

            if (!ModelState.IsValid)
            {
                return View();
            }

            var user = Users.FindByUsernameOrEmailAddressAndPassword(
                request.UserNameOrEmail,
                request.Password);

            if (user == null)
            {
                ModelState.AddModelError(
                    String.Empty,
                    Strings.UserNotFound);

                return View();
            }

            if (!user.Confirmed)
            {
                ViewBag.ConfirmationRequired = true;
                return View();
            }

            FormsAuth.SetAuthCookie(
                user,
                true);

            return SafeRedirect(returnUrl);
        }

        [HttpGet]
        public virtual ActionResult LinkOrCreateUser(string token, string returnUrl)
        {
            // Set the returnURL for the login link.
            ViewData[Constants.ReturnUrlViewDataKey] = returnUrl;

            // Deserialize the token
            OAuthLinkToken linkToken = DecodeToken(token);

            // Send down the view model
            return View(new LinkOrCreateViewModel()
            {
                CreateModel = new LinkOrCreateViewModel.CreateViewModel()
                {
                    Username = Regex.IsMatch(linkToken.UserName, Constants.UserNameRegex) ? linkToken.UserName : null,
                    EmailAddress = linkToken.EmailAddress
                },
                LinkModel = new LinkOrCreateViewModel.LinkViewModel()
                {
                    UserNameOrEmail = linkToken.EmailAddress
                }
            });
        }

        [HttpPost]
        public virtual ActionResult LinkOrCreateUser(LinkOrCreateViewModel model, string token, string returnUrl)
        {
            // Set the returnURL for the login link.
            ViewData[Constants.ReturnUrlViewDataKey] = returnUrl;

            // Don't even bother if the model state is invalid.
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Decode the token
            OAuthLinkToken linkToken = DecodeToken(token);
            
            // Do we have a link token or a create token?
            if (model.LinkModel != null)
            {
                return LinkUser(model, linkToken, returnUrl);
            }
            else if (model.CreateModel != null)
            {
                return CreateUser(model, linkToken, returnUrl);
            }
            return LinkOrCreateUser(token, returnUrl);
        }

        public virtual ActionResult LogOff(string returnUrl)
        {
            // TODO: this should really be a POST

            FormsAuth.SignOut();

            return SafeRedirect(returnUrl);
        }

        [NonAction]
        public virtual ActionResult SafeRedirect(string returnUrl)
        {
            if (!String.IsNullOrWhiteSpace(returnUrl)
                && Url.IsLocalUrl(returnUrl)
                && returnUrl.Length > 1
                && returnUrl.StartsWith("/", StringComparison.Ordinal)
                && !returnUrl.StartsWith("//", StringComparison.Ordinal)
                && !returnUrl.StartsWith("/\\", StringComparison.Ordinal))
            {
                return Redirect(returnUrl);
            }

            return Redirect(Url.Home());
        }

        private OAuthLinkToken DecodeToken(string token)
        {
            return OAuthLinkToken.FromToken(
                            Crypto.DecryptString(token, OAuthLinkToken.CryptoPurpose));
        }

        private ActionResult CreateUser(LinkOrCreateViewModel model, OAuthLinkToken token, string returnUrl)
        {
            Debug.Assert(model.CreateModel != null);
            Debug.Assert(ModelState.IsValid);

            var createModel = model.CreateModel;

            User user;
            try
            {
                user = Users.Create(
                    createModel.Username,
                    createModel.Password,
                    createModel.EmailAddress);
            }
            catch (EntityException ex)
            {
                ModelState.AddModelError(String.Empty, ex.Message);
                return View(model);
            }

            if (user == null)
            {
                throw new InvalidOperationException("UserService failed to create a user.");
            }

            if (!Users.AssociateCredential(user, "oauth:" + token.Provider, token.Id))
            {
                throw new InvalidOperationException("Failed to associate OAuth credential with new user!");
            }

            if (Config.ConfirmEmailAddresses)
            {
                // Passing in scheme to force fully qualified URL
                var confirmationUrl = Url.ConfirmationUrl(
                    MVC.Users.Confirm(), user.Username, user.EmailConfirmationToken, protocol: Request.Url.Scheme);
                Messages.SendNewAccountEmail(new MailAddress(createModel.EmailAddress, user.Username), confirmationUrl);
            }

            return RedirectToAction(MVC.Users.Thanks());
        }

        private ActionResult LinkUser(LinkOrCreateViewModel model, OAuthLinkToken token, string returnUrl)
        {
            Debug.Assert(model.LinkModel != null);
            Debug.Assert(ModelState.IsValid);

            var linkModel = model.LinkModel;

            var user = Users.FindByUsernameOrEmailAddressAndPassword(linkModel.UserNameOrEmail, linkModel.Password);
            if (user == null)
            {
                ModelState.AddModelError(
                    String.Empty,
                    Strings.UserNotFound);
                return View(model);
            }

            if (!user.Confirmed)
            {
                ViewBag.ConfirmationRequired = true;
                return View(model);
            }

            // Associate the user
            if (!Users.AssociateCredential(user, "oauth:" + token.Provider, token.Id))
            {
                // User already has a token of this type!
                ModelState.AddModelError(
                    String.Empty,
                    Strings.DuplicateOAuthCredential);
                return View(model);
            }

            // Log the user in
            FormsAuth.SetAuthCookie(user, createPersistentCookie: true);

            // Safe redirect outta here
            return SafeRedirect(returnUrl);
        }
    }
}
