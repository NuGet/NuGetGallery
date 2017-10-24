// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
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

        public class LegalLinkActions : TestContainer
        {
            [Fact]
            public async Task ExternalPrivacyPolicyUrlRedirects()
            {
                var customUrl = "http://privacyPolicy";
                var configuration = GetConfigurationService();
                configuration.Current.ExternalPrivacyPolicyUrl = customUrl;

                var controller = GetController<PagesController>();
                var result = await controller.Privacy();

                Assert.IsType<RedirectResult>(result);
                Assert.Equal(customUrl, ((RedirectResult)result).Url);
            }

            [Fact]
            public async Task NoExternalPrivacyPolicyUrlDoesNotRedirect()
            {
                var configuration = GetConfigurationService();

                var controller = GetController<PagesController>();
                var result = await controller.Privacy();

                Assert.IsType<ViewResult>(result);
            }

            [Fact]
            public async Task ExternalTermsOfUseUrlRedirects()
            {
                var customUrl = "http://TermsOfUse";
                var configuration = GetConfigurationService();
                configuration.Current.ExternalTermsOfUseUrl = customUrl;

                var controller = GetController<PagesController>();
                var result = await controller.Terms();

                Assert.IsType<RedirectResult>(result);
                Assert.Equal(customUrl, ((RedirectResult)result).Url);
            }

            [Fact]
            public async Task NoExternalTermsOfUseUrlDoesNotRedirect()
            {
                var configuration = GetConfigurationService();

                var controller = GetController<PagesController>();
                var result = await controller.Terms();

                Assert.IsType<ViewResult>(result);
            }

            [Fact]
            public void ExternalAboutUrlRedirects()
            {
                var customUrl = "http://About";
                var configuration = GetConfigurationService();
                configuration.Current.ExternalAboutUrl = customUrl;

                var controller = GetController<PagesController>();
                var result = controller.About();

                Assert.IsType<RedirectResult>(result);
                Assert.Equal(customUrl, ((RedirectResult)result).Url);
            }

            [Fact]
            public void NoExternalAboutUrlDoesNotRedirect()
            {
                var configuration = GetConfigurationService();

                var controller = GetController<PagesController>();
                var result = controller.About();

                Assert.IsType<ViewResult>(result);
            }
        }
    }
}