﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using System.Web;
using Moq;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Framework;
using NuGetGallery.Services;
using NuGetGallery.ViewModels;
using Xunit;

namespace NuGetGallery
{
    public class PagesControllerFacts
    {
        public class TheContactAction : TestContainer
        {
            [Fact]
            public async Task HtmlEncodesTheSupportContactEmail()
            {
                // arrage: the contact form, the expected encoding, and setup a user
                var contactForm = new ContactSupportViewModel
                {
                    Message = "<strong>Something with HTML in it</strong>",
                    SubjectLine = "<script>alert('malicious javascript perhaps')</script>"
                };

                var expectedMessage = HttpUtility.HtmlEncode(contactForm.Message);
                var expectedSubjectLine = HttpUtility.HtmlEncode(contactForm.SubjectLine);

                var controller = GetController<PagesController>();
                // Have to set this up first because it needs current user
                controller.SetCurrentUser(new User
                {
                    Username = "aUsername",
                    UnconfirmedEmailAddress = "old@example.com",
                    EmailConfirmationToken = "aToken",
                });

                // act: run the controller action
                await controller.Contact(contactForm);

                // assert: the HTML encoded message was passed to the service
                GetMock<IMessageService>()
                    .Verify(m => m.SendContactSupportEmail(
                        It.Is<ContactSupportRequest>(c =>
                            c.Message == expectedMessage
                            && c.SubjectLine == expectedSubjectLine)));
            }

            [Fact]
            public async Task HtmlEncodesTheSupportRequest()
            {
                // arrage: the contact form, the expected encoding, and setup a user
                var contactForm = new ContactSupportViewModel
                {
                    Message = "<b>some html</b>",
                    SubjectLine = "maybe some malicious javascript: <script>alert('teh XSS hax')</script>"
                };

                var expectedMessage = HttpUtility.HtmlEncode(contactForm.Message);

                var controller = GetController<PagesController>();
                // Have to set this up first because it needs current user
                controller.SetCurrentUser(new User
                {
                    Username = "aUsername",
                    UnconfirmedEmailAddress = "old@example.com",
                    EmailConfirmationToken = "aToken",
                });

                // act: run the controller action
                await controller.Contact(contactForm);

                // assert: the HTML encoded message was passed to the service
                GetMock<ISupportRequestService>()
                    .Verify(m => m.AddNewSupportRequestAsync(
                        It.IsAny<string>(),
                        expectedMessage,
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<User>(),
                        It.IsAny<Package>()));
            }
        }
    }
}