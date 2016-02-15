// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Services;
using NuGetGallery.ViewModels;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace NuGetGallery
{
    public partial class PagesController
        : AppController
    {
        public IContentService ContentService { get; protected set; }
        public IMessageService MessageService { get; protected set; }

        protected PagesController() { }
        public PagesController(IContentService contentService, IMessageService messageService)
        {
            ContentService = contentService;
            MessageService = messageService;
        }

        // This will let you add 'static' cshtml pages to the site under View/Pages or Branding/Views/Pages
        public virtual ActionResult Page(string pageName)
        {
            // Prevent traversal attacks and serving non-pages by disallowing ., /, %, and more!
            if (pageName == null || pageName.Any(c => !Char.IsLetterOrDigit(c)))
            {
                return HttpNotFound();
            }

            return View(pageName);
        }

        public virtual ActionResult About()
        {
            return View();
        }

        public virtual ActionResult Contact()
        {
            return View();
        }

        [Authorize]
        [HttpPost]
        public virtual ActionResult Contact(ContactSupportViewModel contactForm)
        {
            if (!ModelState.IsValid)
            {
                return View();
            }

            User user = GetCurrentUser();
            var request = new ContactSupportRequest
            {
                CopySender = contactForm.CopySender,
                Message = contactForm.Message,
                SubjectLine = contactForm.SubjectLine,
                FromAddress = user.ToMailAddress(),
                RequestingUser = user
            };
            MessageService.SendContactSupportEmail(request);

            ModelState.Clear();
            TempData["Message"] = "Your message has been sent to support. We'll be in contact with you shortly.";
            return View();
        }

        public virtual async Task<ActionResult> Home()
        {
            if (ContentService != null)
            {
                ViewBag.Content = await ContentService.GetContentItemAsync(
                    Constants.ContentNames.Home,
                    TimeSpan.FromMinutes(1));
            }
            return View();
        }

        public virtual ActionResult EmptyHome()
        {
            return new HttpStatusCodeResult(HttpStatusCode.OK, "Empty Home");
        }

        public virtual async Task<ActionResult> Terms()
        {
            if (ContentService != null)
            {
                ViewBag.Content = await ContentService.GetContentItemAsync(
                    Constants.ContentNames.TermsOfUse,
                    TimeSpan.FromDays(1));
            }
            return View();
        }

        public virtual async Task<ActionResult> Privacy()
        {
            if (ContentService != null)
            {
                ViewBag.Content = await ContentService.GetContentItemAsync(
                    Constants.ContentNames.PrivacyPolicy,
                    TimeSpan.FromDays(1));
            }
            return View();
        }
    }
}