// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Jobs.Configuration;

namespace Gallery.CredentialExpiration
{
    public class InitializationConfiguration : MessageServiceConfiguration
    {
        public string ContainerName { get; set; }

        public string DataStorageAccount { get; set; }

        public string EmailPublisherConnectionString { get; set; }

        public string EmailPublisherTopicName { get; set; }

        public string GalleryAccountUrl { get; set; }

        public string GalleryBrand { get; set; }

        public string MailFrom { get; set; }

        public int WarnDaysBeforeExpiration { get; set; }

        public bool WhatIf { get; set; }
    }
}
