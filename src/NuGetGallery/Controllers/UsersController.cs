using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Principal;
using System.Web.Mvc;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public partial class UsersController : AppController
    {
        public ICuratedFeedService CuratedFeedService { get; protected set; }
        public IMessageService MessageService { get; protected set; }
        public IPackageService PackageService { get; protected set; }
        public IAppConfiguration Config { get; protected set; }
        public IUserService UserService { get; protected set; }

        protected UsersController() { }

        public UsersController(
            ICuratedFeedService feedsQuery,
            IUserService userService,
            IPackageService packageService,
            IMessageService messageService,
            IAppConfiguration config) : this()
        {
            CuratedFeedService = feedsQuery;
            UserService = userService;
            PackageService = packageService;
            MessageService = messageService;
            Config = config;
        }

        [Authorize]
        public virtual ActionResult Account()
        {
            var user = UserService.FindByUsername(Identity.Name);
            var curatedFeeds = CuratedFeedService.GetFeedsForManager(user.Key);
            return View(
                new AccountViewModel
                    {
                        ApiKey = user.ApiKey.ToString(),
                        IsConfirmed = user.Confirmed,
                        CuratedFeeds = curatedFeeds.Select(cf => cf.Name)
                    });
        }

        [Authorize]
        [HttpGet]
        public virtual ActionResult ConfirmationRequired()
        {
            User user = UserService.FindByUsername(User.Identity.Name);
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
            User user = UserService.FindByUsername(User.Identity.Name);
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
            var user = UserService.FindByUsername(Identity.Name);
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
            var user = UserService.FindByUsername(Identity.Name);
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
            var user = UserService.FindByUsername(Identity.Name);
            var packages = PackageService.FindPackagesByOwner(user);

            var model = new ManagePackagesViewModel
                {
                    Packages = from p in packages
                               select new PackageViewModel(p)
                                   {
                                       DownloadCount = p.PackageRegistration.DownloadCount,
                                       Version = null
                                   },
                };
            return View(model);
        }

        [Authorize]
        [ValidateAntiForgeryToken]
        [HttpPost]
        public virtual ActionResult GenerateApiKey()
        {
            UserService.GenerateApiKey(Identity.Name);
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
                var user = UserService.GeneratePasswordResetToken(model.Email, Constants.DefaultPasswordResetTokenExpirationHours * 60);
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
            
            ViewBag.ResetTokenValid = UserService.ResetPasswordWithToken(username, token, model.NewPassword);

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

            if (!String.Equals(username, Identity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return View(new ConfirmationViewModel
                    {
                        WrongUsername = true,
                        SuccessfulConfirmation = false,
                    });
            }

            var user = UserService.FindByUsername(username);
            if (user == null)
            {
                return HttpNotFound();
            }

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

            var packages = (from p in PackageService.FindPackagesByOwner(user)
                            where p.Listed
                            orderby p.Version descending
                            group p by p.PackageRegistration.Id)
                .Select(c => new PackageViewModel(c.First()))
                .ToList();

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

            User user = UserService.FindByUsernameAndPassword(Identity.Name, model.Password);
            if (user == null)
            {
                ModelState.AddModelError("Password", Strings.CurrentPasswordIncorrect);
                return View(model);
            }

            if (String.Equals(model.NewEmail, user.LastSavedEmailAddress, StringComparison.OrdinalIgnoreCase))
            {
                // email address unchanged - accept
                return RedirectToAction(MVC.Users.Edit());
            }

            try
            {
                UserService.ChangeEmailAddress(user, model.NewEmail);
            }
            catch (EntityException e)
            {
                ModelState.AddModelError("NewEmail", e.Message);
                return View(model);
            }

            if (user.Confirmed)
            {
                var confirmationUrl = Url.ConfirmationUrl(
                    MVC.Users.Confirm(), user.Username, user.EmailConfirmationToken, protocol: Request.Url.Scheme);
                MessageService.SendEmailChangeConfirmationNotice(new MailAddress(user.UnconfirmedEmailAddress, user.Username), confirmationUrl);

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
        [ValidateAntiForgeryToken]
        [Authorize]
        public virtual ActionResult ChangePassword(PasswordChangeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!UserService.ChangePassword(Identity.Name, model.OldPassword, model.NewPassword))
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
