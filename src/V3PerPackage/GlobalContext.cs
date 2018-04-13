// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.V3PerPackage
{
    /// <summary>
    /// This contains information that is relevant for all processes of this executable.
    /// </summary>
    public class GlobalContext
    {
        public GlobalContext(string storageBaseAddress, string storageAccountName, string storageKeyValue, Uri contentBaseAddress)
        {
            StorageBaseAddress = storageBaseAddress;
            StorageAccountName = storageAccountName;
            StorageKeyValue = storageKeyValue;
            ContentBaseAddress = contentBaseAddress;
        }

        public string StorageBaseAddress { get; }
        public string StorageAccountName { get; }
        public string StorageKeyValue { get; }
        public Uri ContentBaseAddress { get; }

        public string CatalogContainerName => "v3-catalog";
        public string FlatContainerContainerName => "v3-flatcontainer";
        public string RegistrationContainerName => "v3-registration";
    }
}
