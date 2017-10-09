// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Configuration;
using NuGetGallery.Services;
using NuGetGallery.ViewModels;

namespace NuGetGallery
{
    public partial class PagesController
        : AppController
    {
        private readonly IContentService _contentService;
        private readonly IGalleryConfigurationService _galleryConfigurationService;
        private readonly IMessageService _messageService;
        private readonly ISupportRequestService _supportRequestService;

        protected PagesController() { }
        public PagesController(IContentService contentService,
            IGalleryConfigurationService galleryConfigurationService,
            IMessageService messageService,
            ISupportRequestService supportRequestService)
        {
            _contentService = contentService;
            _galleryConfigurationService = galleryConfigurationService;
            _messageService = messageService;
            _supportRequestService = supportRequestService;
        }

        // This will let you add 'static' cshtml pages to the site under View/Pages or Branding/Views/Pages
        [HttpGet]
        public virtual ActionResult Page(string pageName)
        {
            // Prevent traversal attacks and serving non-pages by disallowing ., /, %, and more!
            if (pageName == null || pageName.Any(c => !Char.IsLetterOrDigit(c)))
            {
                return HttpNotFound();
            }

            return View(pageName);
        }

        [HttpGet]
        public virtual ActionResult About()
        {
            if (!String.IsNullOrEmpty(_galleryConfigurationService.Current.ExternalAboutUrl))
            {
                return Redirect(_galleryConfigurationService.Current.ExternalAboutUrl);
            }

            return View();
        }

        [HttpGet]
        public virtual ActionResult Contact()
        {
            return View(new ContactSupportViewModel());
        }

        [HttpGet]
        public virtual ActionResult Downloads()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> Contact(ContactSupportViewModel contactForm)
        {
            if (!ModelState.IsValid)
            {
                return View(contactForm);
            }

            // since HTML is allowed in these fields, encode it to avoid malicious HTML
            contactForm.Message = HttpUtility.HtmlEncode(contactForm.Message);
            contactForm.SubjectLine = HttpUtility.HtmlEncode(contactForm.SubjectLine);

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

        public virtual ActionResult Home()
        {
            return View();
        }

        [HttpGet]
        public virtual ActionResult EmptyHome()
        {
            return new HttpStatusCodeResult(HttpStatusCode.OK, "Empty Home");
        }

        public virtual async Task<ActionResult> Terms()
        {
            if (!String.IsNullOrEmpty(_galleryConfigurationService.Current.ExternalTermsOfUseUrl))
            {
                return Redirect(_galleryConfigurationService.Current.ExternalTermsOfUseUrl);
            }

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
            if (!String.IsNullOrEmpty(_galleryConfigurationService.Current.ExternalPrivacyPolicyUrl))
            {
                return Redirect(_galleryConfigurationService.Current.ExternalPrivacyPolicyUrl);
            }

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