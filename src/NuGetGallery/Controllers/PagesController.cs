// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Services;
using NuGetGallery.ViewModels;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin;

namespace NuGetGallery
{
    public partial class PagesController
        : AppController
    {
        private readonly IContentService _contentService;
        private readonly IMessageService _messageService;
        private readonly ISupportRequestService _supportRequestService;

        protected PagesController() { }
        public PagesController(IContentService contentService,
            IMessageService messageService,
            ISupportRequestService supportRequestService)
        {
            _contentService = contentService;
            _messageService = messageService;
            _supportRequestService = supportRequestService;
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

        public virtual ActionResult Downloads()
        {
            return Redirect("https://dist.nuget.org/index.html");
        }

        [Authorize]
        [HttpPost]
        public virtual async Task<ActionResult> Contact(ContactSupportViewModel contactForm)
        {
            if (!ModelState.IsValid)
            {
                return View();
            }

            var user = GetCurrentUser();
            var request = new ContactSupportRequest
            {
                CopySender = contactForm.CopySender,
                Message = contactForm.Message,
                SubjectLine = contactForm.SubjectLine,
                FromAddress = user.ToMailAddress(),
                RequestingUser = user
            };

            var subject = $"Support Request for user '{user.Username}'";
            await _supportRequestService.AddNewSupportRequestAsync(subject, contactForm.Message, user.EmailAddress, "Other", user);

            _messageService.SendContactSupportEmail(request);

            ModelState.Clear();

            TempData["Message"] = "Your message has been sent to support. We'll be in contact with you shortly.";

            return View();
        }

        public virtual async Task<ActionResult> Home()
        {
            if (_contentService != null)
            {
                var homeContent = await _contentService.GetContentItemAsync(
                     Constants.ContentNames.Home,
                     TimeSpan.FromMinutes(1));

                homeContent = new HtmlString(homeContent.ToString().Replace("~/", Url.Content("~/")));

                ViewBag.Content = homeContent;
            }
            return View();
        }

        public virtual ActionResult EmptyHome()
        {
            return new HttpStatusCodeResult(HttpStatusCode.OK, "Empty Home");
        }

        public virtual async Task<ActionResult> Terms()
        {
            if (_contentService != null)
            {
                ViewBag.Content = await _contentService.GetContentItemAsync(
                    Constants.ContentNames.TermsOfUse,
                    TimeSpan.FromDays(1));
            }
            return View();
        }

        public virtual async Task<ActionResult> Privacy()
        {
            if (_contentService != null)
            {
                ViewBag.Content = await _contentService.GetContentItemAsync(
                    Constants.ContentNames.PrivacyPolicy,
                    TimeSpan.FromDays(1));
            }
            return View();
        }
    }
}