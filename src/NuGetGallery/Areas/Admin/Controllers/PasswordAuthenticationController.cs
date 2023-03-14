using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Login;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class PasswordAuthenticationController : AdminControllerBase
    {
        private readonly IUserService _userService;
        private readonly IEditableLoginConfigurationFileStorageService _storage;

        public PasswordAuthenticationController(
            IUserService userService,
            IEditableLoginConfigurationFileStorageService storage)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        [HttpGet]
        public async virtual Task<ActionResult> Index()
        {
            return View(
                new ExceptionEmailListViewModel
                {
                    EmailList = await _storage.GetListOfExceptionEmailList()
                });
        }

        [HttpGet]
        public ActionResult Search(string query)
        {
            var results = new List<UserCredentialSearchResult>();
            if (!string.IsNullOrWhiteSpace(query))
            {
                User user;
                if (Helpers.IsValidEmail(query))
                {
                    user = _userService.FindByEmailAddress(query);
                }
                else
                {
                    user = _userService.FindByUsername(query);            
                }

                if (user != null)
                {
                    var result = new UserCredentialSearchResult()
                    {
                        Username = user.Username,
                        EmailAddress = user.EmailAddress,
                    };
                         
                    result.Credential = new UserCredential()
                    {
                        TenantId = user.Credentials.GetAzureActiveDirectoryCredential()?.TenantId,
                        Value = user.Credentials.GetAzureActiveDirectoryCredential()?.Value,
                        Type = user.Credentials.GetMicrosoftAccountCredential()?.Type

                    };
                    results.Add(result);
                }
            }

            return Json(results, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddEmailAddress(string emailAddress)
        {
            var contentId = await GetContentIdBeforeChange();
            var errorMessage = await TrySaveFlags(emailAddress, contentId, add: true);

            if (errorMessage != null)
            {
                TempData["ErrorMessage"] = errorMessage;
            }

            return Redirect(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RemoveEmailAddress(string emailAddress) { 
        
            var contentId = await GetContentIdBeforeChange();
            var errorMessage = await TrySaveFlags(emailAddress, contentId, add: false);

            if (errorMessage != null)
            {
                TempData["ErrorMessage"] = errorMessage;
            }

            return Redirect(nameof(Index));

        }

        private async Task<string> GetContentIdBeforeChange()
        {
            var result = await _storage.GetReferenceAsync(); 
            return result.ContentId;
        }

        private async Task<string> TrySaveFlags(string emailAddress, string contentId, bool add)
        {

           await _storage.AddUserEmailAddressforPasswordAuthenticationAsync(emailAddress, add);
           return null;
        }
    }
}