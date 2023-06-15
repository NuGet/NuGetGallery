// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Web.Mvc;
using Microsoft.Ajax.Utilities;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Login;
using NuGetGallery.Shared;

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
                        IsAADorMACredential = false
                    };

                    Credential microftAccountOrAADCredentail = user.Credentials.GetMicrosoftAccountCredential() ?? user.Credentials.GetAzureActiveDirectoryCredential();

                    if (microftAccountOrAADCredentail != null)
                    {
                        result.IsAADorMACredential = true;
                        result.Credential = GetMAorAADCredential(microftAccountOrAADCredentail);
                    }

                    results.Add(result);
                }
            }

            return Json(results, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddEmailAddress(string emailAddress)
        {
            return await AddOrRemoveEmailAddress(emailAddress, ContentOperations.Add);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RemoveEmailAddress(string emailAddress) {
            return await AddOrRemoveEmailAddress(emailAddress, ContentOperations.Remove);
        }

        private async Task<ActionResult> AddOrRemoveEmailAddress(string emailAddress, ContentOperations operation)
        {
            if (emailAddress.IsNullOrWhiteSpace())
            {
                TempData["ErrorMessage"] = "Email address cannot be empty!";
                return Redirect(nameof(Index));
            }

            var emailAddressTrimed = emailAddress.Trim();
            var user = _userService.FindByEmailAddress(emailAddressTrimed);
            if (user == null)
            {
                TempData["ErrorMessage"] = $"User with emailAdress '{emailAddressTrimed}' does not exist!";
                return Redirect(nameof(Index));
            }

            if (user is Organization)
            {
                TempData["ErrorMessage"] = $"User with emailAdress '{emailAddressTrimed}' is an organization, we don't support organization password authentication!";
                return Redirect(nameof(Index));
            }

            var contentId = await GetContentIdBeforeChange();
            await TrySaveEmailAddress(emailAddressTrimed, operation);

            return Redirect(nameof(Index));
        }

        private async Task<string> GetContentIdBeforeChange()
        {
            var result = await _storage.GetReferenceAsync(); 
            return result.ContentId;
        }

        private async Task TrySaveEmailAddress(string emailAddress, ContentOperations operation)
        {
           await _storage.AddUserEmailAddressforPasswordAuthenticationAsync(emailAddress, operation);
        }

        private UserCredential GetMAorAADCredential(Credential microftAccountOrAADCredentail)
        {
            var userCredential = new UserCredential()
            {
                TenantId = microftAccountOrAADCredentail.TenantId,
                Value = microftAccountOrAADCredentail.Value,
                Type = microftAccountOrAADCredentail.Type
            };

            return userCredential;
        }
    }
}