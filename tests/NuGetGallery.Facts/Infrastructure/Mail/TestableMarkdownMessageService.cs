// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using Moq;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure.Mail;
using NuGetGallery.Services.Telemetry;

namespace NuGetGallery
{
    public class TestableMarkdownMessageService
        : MarkdownMessageService
    {
        public static readonly MailAddress TestGalleryOwner = new MailAddress("joe@example.com", "Joe Shmoe");
        public static readonly MailAddress TestGalleryNoReplyAddress = new MailAddress("noreply@example.com", "No Reply");

        private TestableMarkdownMessageService(IGalleryConfigurationService configurationService)
            : base(new TestMailSender(), configurationService.Current, new Mock<ITelemetryService>().Object)
        {
            configurationService.Current.GalleryOwner = TestGalleryOwner;
            configurationService.Current.GalleryNoReplyAddress = TestGalleryNoReplyAddress;

            MockMailSender = (TestMailSender)MailSender;
        }

        public Mock<AuthenticationService> MockAuthService { get; protected set; }
        public TestMailSender MockMailSender { get; protected set; }

        public static TestableMarkdownMessageService Create(IGalleryConfigurationService configurationService)
        {
            configurationService.Current.SmtpUri = new Uri("smtp://fake.mail.server");
            return new TestableMarkdownMessageService(configurationService);
        }
    }
}