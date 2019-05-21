// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.AzureManagement;

namespace NuGet.Jobs.Montoring.PackageLag
{
    public class AzureManagementAPIWrapperConfiguration : IAzureManagementAPIWrapperConfiguration
    {
        public string ClientId { get; set; }

        public string ClientSecret { get; set; }

        public string AadTenant { get; set; }
    }
}
