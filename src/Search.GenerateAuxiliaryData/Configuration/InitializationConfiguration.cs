// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Search.GenerateAuxiliaryData
{
    public class InitializationConfiguration
    {
        public string AzureCdnCloudStorageAccount { get; set; }

        public string AzureCdnCloudStorageContainerName { get; set; }

        public string PrimaryDestination { get; set; }

        public string DestinationContainerName { get; set; }
    }
}
