// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Mail;

namespace NuGet.Services.Messaging.Email
{
    public interface IMessageServiceConfiguration
    {
        /// <summary>
        /// Gets the gallery owner name and email address
        /// </summary>
        MailAddress GalleryOwner { get; set; }

        /// <summary>
        /// Gets the gallery e-mail from name and email address
        /// </summary>
        MailAddress GalleryNoReplyAddress { get; set; }
    }
}