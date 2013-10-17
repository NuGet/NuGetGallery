using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Principal;
using System.Web.Mvc;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public partial class UsersController : AppController
    {
        public ICuratedFeedService CuratedFeedService { get; protected set; }
        public IUserService UserService { get; protected set; }
        public IMessageService MessageService { get; protected set; }
        public IPackageService PackageService { get; protected set; }
        public IAppConfiguration Config { get; protected set; }
        public AuthenticationService AuthService { get; protected set; }

        protected UsersController() { }

        public UsersController(
            ICuratedFeedService feedsQuery,
            IUserService userService,
            IPackageService packageService,
            IMessageService messageService,
            IAppConfiguration config,
            AuthenticationService authService) : this()
        {
            CuratedFeedService = feedsQuery;
            UserService = userService;
            PackageService = packageService;
            MessageService = messageService;
            Config = config;
            AuthService = authService;
        }

        [Authorize]
        public virtual ActionResult Account()
        {
            var user = GetCurrentUser();
            var curatedFeeds = CuratedFeedService.GetFeedsForManager(user.Key);
            var apiCredential = user
                .Credentials
                .FirstOrDefault(c => c.Type == CredentialTypes.ApiKeyV1);
            return View(
                new AccountViewModel
                    {
                        ApiKey = apiCredential == null ? 
                            user.ApiKey.ToString() :
                            apiCredential.Value,
                        IsConfirmed = user.Confirmed,
                        CuratedFeeds = curatedFeeds.Select(cf => cf.Name)
                    });
        }

        [HttpGet]
        [Authorize]
        public virtual ActionResult ConfirmationRequired()
        {
            User user = GetCurrentUser();
            var model = new ConfirmationViewModel
            {
                ConfirmingNewAccount = !(user.Confirmed),
                UnconfirmedEmailAddress = user.UnconfirmedEmailAddress,
            };
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ActionName("ConfirmationRequired")]
        public virtual ActionResult ConfirmationRequiredPost()
        {
            User user = GetCurrentUser();
            var confirmationUrl = Url.ConfirmationUrl(
                MVC.Users.Confirm(), user.Username, user.EmailConfirmationToken, protocol: Request.Url.Scheme);

            MessageService.SendNewAccountEmail(new MailAddress(user.UnconfirmedEmailAddress, user.Username), confirmationUrl);

            var model = new ConfirmationViewModel
            {
                ConfirmingNewAccount = !(user.Confirmed),
                UnconfirmedEmailAddress = user.UnconfirmedEmailAddress,
                SentEmail = true,
            };
            return View(model);
        }

        [Authorize]
        public virtual ActionResult Edit()
        {
            var user = GetCurrentUser();
            var model = new EditProfileViewModel
                {
                    Username = user.Username,
                    EmailAddress = user.EmailAddress,
                    EmailAllowed = user.EmailAllowed,
                    PendingNewEmailAddress = user.UnconfirmedEmailAddress
                };
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual ActionResult Edit(EditProfileViewModel profile)
        {
            var user = GetCurrentUser();
            if (user == null)
            {
                return HttpNotFound();
            }

            profile.EmailAddress = user.EmailAddress;
            profile.Username = user.Username;
            profile.PendingNewEmailAddress = user.UnconfirmedEmailAddress;
            UserService.UpdateProfile(user, profile.EmailAllowed);
            return View(profile);
        }

        public virtual ActionResult Thanks()
        {
            // No need to redirect here after someone logs in...
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = null;
            return View();
        }

        [Authorize]
        public virtual ActionResult Packages()
        {
            var user = GetCurrentUser();
            var packages = PackageService.FindPackagesByOwner(user, includeUnlisted: true)
                .Select(p => new PackageViewModel(p)
                {
                    DownloadCount = p.PackageRegistration.DownloadCount,
                    Version = null
                }).ToList();

            var model = new ManagePackagesViewModel
            {
                Packages = packages
            };
            return View(model);
        }

        [Authorize]
        [ValidateAntiForgeryToken]
        [HttpPost]
        public virtual ActionResult GenerateApiKey()
        {
            // Get the user
            var user = GetCurrentUser();

            // Generate an API Key
            var apiKey = Guid.NewGuid();

            // Set the existing API Key field
            user.ApiKey = apiKey;

            // Add/Replace the API Key credential, and save to the database
            AuthService.ReplaceCredential(user, CredentialBuilder.CreateV1ApiKey(apiKey));
            return RedirectToAction(MVC.Users.Account());
        }

        public virtual ActionResult ForgotPassword()
        {
            // We don't want Login to have us as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = null;
            
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual ActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            // We don't want Login to have us as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = null;
            
            if (ModelState.IsValid)
            {
                var user = AuthService.GeneratePasswordResetToken(model.Email, Constants.DefaultPasswordResetTokenExpirationHours * 60);
                if (user != null)
                {
                    var resetPasswordUrl = Url.ConfirmationUrl(
                        MVC.Users.ResetPassword(), user.Username, user.PasswordResetToken, protocol: Request.Url.Scheme);
                    MessageService.SendPasswordResetInstructions(user, resetPasswordUrl);

                    TempData["Email"] = user.EmailAddress;
                    return RedirectToAction(MVC.Users.PasswordSent());
                }

                ModelState.AddModelError("Email", "Could not find anyone with that email.");
            }

            return View(model);
        }

        public virtual ActionResult PasswordSent()
        {
            // We don't want Login to have us as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = null;
            
            ViewBag.Email = TempData["Email"];
            ViewBag.Expiration = Constants.DefaultPasswordResetTokenExpirationHours;
            return View();
        }

        public virtual ActionResult ResetPassword()
        {
            // We don't want Login to have us as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = null;
            
            ViewBag.ResetTokenValid = true;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual ActionResult ResetPassword(string username, string token, PasswordResetViewModel model)
        {
            // We don't want Login to have us as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = null;
            
            ViewBag.ResetTokenValid = AuthService.ResetPasswordWithToken(username, token, model.NewPassword);

            if (!ViewBag.ResetTokenValid)
            {
                ModelState.AddModelError("", "The Password Reset Token is not valid or expired.");
                return View(model);
            }
            return RedirectToAction(MVC.Users.PasswordChanged());
        }

        [Authorize]
        public virtual ActionResult Confirm(string username, string token)
        {
            // We don't want Login to have us as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = null;

            if (!String.Equals(username, User.Identity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return View(new ConfirmationViewModel
                    {
                        WrongUsername = true,
                        SuccessfulConfirmation = false,
                    });
            }

            var user = GetCurrentUser();
            
            string existingEmail = user.EmailAddress;
            var model = new ConfirmationViewModel
            {
                ConfirmingNewAccount = String.IsNullOrEmpty(existingEmail),
                SuccessfulConfirmation = true,
            };

            try
            {
                if (!UserService.ConfirmEmailAddress(user, token))
                {
                    model.SuccessfulConfirmation = false;
                }
            }
            catch (EntityException)
            {
                model.SuccessfulConfirmation = false;
                model.DuplicateEmailAddress = true;
            }

            // SuccessfulConfirmation is required so that the confirm Action isn't a way to spam people.
            // Change notice not required for new accounts.
            if (model.SuccessfulConfirmation && !model.ConfirmingNewAccount)
            {
                MessageService.SendEmailChangeNoticeToPreviousEmailAddress(user, existingEmail);

                string returnUrl = HttpContext.GetConfirmationReturnUrl();
                if (!String.IsNullOrEmpty(returnUrl))
                {
                    TempData["Message"] = "You have successfully confirmed your email address!";
                    return new RedirectResult(RedirectHelper.SafeRedirectUrl(Url, returnUrl));
                }
            }

            return View(model);
        }

        public virtual ActionResult Profiles(string username)
        {
            var user = UserService.FindByUsername(username);
            if (user == null)
            {
                return HttpNotFound();
            }

            var packages = PackageService.FindPackagesByOwner(user, includeUnlisted: false)
                .Select(p => new PackageViewModel(p)
                {
                    DownloadCount = p.PackageRegistration.DownloadCount,
                    Version = null
                }).ToList();

            var model = new UserProfileModel(user)
            {
                Packages = packages,
                TotalPackageDownloadCount = packages.Sum(p => p.TotalDownloadCount),
            };

            return View(model);
        }

        [Authorize]
        public virtual ActionResult ChangeEmail()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        public virtual ActionResult ChangeEmail(ChangeEmailRequestModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var authUser = AuthService.Authenticate(User.Identity.Name, model.Password);
            if (authUser == null)
            {
                ModelState.AddModelError("Password", Strings.CurrentPasswordIncorrect);
                return View(model);
            }

            if (String.Equals(model.NewEmail, authUser.User.LastSavedEmailAddress, StringComparison.OrdinalIgnoreCase))
            {
                // email address unchanged - accept
                return RedirectToAction(MVC.Users.Edit());
            }

            try
            {
                UserService.ChangeEmailAddress(authUser.User, model.NewEmail);
            }
            catch (EntityException e)
            {
                ModelState.AddModelError("NewEmail", e.Message);
                return View(model);
            }

            if (authUser.User.Confirmed)
            {
                var confirmationUrl = Url.ConfirmationUrl(
                    MVC.Users.Confirm(), authUser.User.Username, authUser.User.EmailConfirmationToken, protocol: Request.Url.Scheme);
                MessageService.SendEmailChangeConfirmationNotice(new MailAddress(authUser.User.UnconfirmedEmailAddress, authUser.User.Username), confirmationUrl);

                TempData["Message"] =
                    "Your email address has been changed! We sent a confirmation email to verify your new email. When you confirm the new email address, it will take effect and we will forget the old one.";
            }
            else
            {
                TempData["Message"] = "Your new email address was saved!";
            }

            return RedirectToAction(MVC.Users.Edit());
        }

        [Authorize]
        public virtual ActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public virtual ActionResult ChangePassword(PasswordChangeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!AuthService.ChangePassword(User.Identity.Name, model.OldPassword, model.NewPassword))
            {
                ModelState.AddModelError(
                    "OldPassword",
                    Strings.CurrentPasswordIncorrect);

                return View(model);
            }

            return RedirectToAction(MVC.Users.PasswordChanged());
        }

        public virtual ActionResult PasswordChanged()
        {
            return View();
        }
    }
}
