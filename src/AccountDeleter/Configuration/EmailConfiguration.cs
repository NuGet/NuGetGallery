// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Jobs.Configuration;

namespace NuGetGallery.AccountDeleter
{
    public class EmailConfiguration
    {
        /// <summary>
        /// The Service Bus configuration used to enqueue emails for sending.
        /// </summary>
        public ServiceBusConfiguration ServiceBus { get; set; }

        /// <summary>
        /// Gallery owner name and email looking like "Gallery Owner <admin@my.org>"
        /// </summary>
        public string GalleryOwner { get; set; }

        /// <summary>
        /// No-reply name and address to use in emails that should not be answered,
        /// should be in form of "No Reply <noreply@my.org>"
        /// </summary>
        public string GalleryNoReplyAddress { get; set; }

        /// <summary>
        /// A template to be used to generate package URLs. Should contain two placeholders:
        /// {0} - for the package id
        /// {1} - for the normalized package version
        /// </summary>
        public string PackageUrlTemplate { get; set; }

        /// <summary>
        /// A template to be used to generate the package support URL. Should contain two placeholders:
        /// {0} - for the package id
        /// {1} - for the normalized package version
        /// </summary>
        public string PackageSupportTemplate { get; set; }

        /// <summary>
        /// Url for email settings, so user can opt out of receiving email notifications.
        /// </summary>
        public string EmailSettingsUrl { get; set; }

        /// <summary>
        /// Url for the announcements github page
        /// </summary>
        public string AnnouncementsUrl { get; set; }

        /// <summary>
        /// NuGet Twitter url
        /// </summary>
        public string TwitterUrl { get; set; }
    }
}
