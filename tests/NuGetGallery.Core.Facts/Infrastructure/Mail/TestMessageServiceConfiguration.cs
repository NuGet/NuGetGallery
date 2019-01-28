// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Mail;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail
{
    public class TestMessageServiceConfiguration
        : IMessageServiceConfiguration
    {
        public TestMessageServiceConfiguration()
        {
            GalleryOwner = new MailAddress("owner@gallery.org", "NuGetGallery");
            GalleryNoReplyAddress = new MailAddress("noreply@gallery.org", "NuGetGallery No-Reply");
        }
        public MailAddress GalleryOwner { get; set; }
        public MailAddress GalleryNoReplyAddress { get; set; }
    }
}