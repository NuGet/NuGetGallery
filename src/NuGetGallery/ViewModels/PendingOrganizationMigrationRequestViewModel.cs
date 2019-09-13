// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class PendingOrganizationMigrationRequestViewModel
    {
        public PendingOrganizationMigrationRequestViewModel(OrganizationMigrationRequest request)
        {
            AdminUsername = request.AdminUser.Username;
            ConfirmationToken = request.ConfirmationToken;
        }

        public string AdminUsername { get; }
        public string ConfirmationToken { get; }
    }
}