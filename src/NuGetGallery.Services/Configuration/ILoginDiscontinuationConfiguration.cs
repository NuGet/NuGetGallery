// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using NuGetGallery.Authentication;

namespace NuGetGallery.Configuration
{
    public interface ILoginDiscontinuationConfiguration
    {
        bool IsLoginDiscontinued(AuthenticatedUser authUser);

        bool IsPasswordLoginDiscontinuedForAll();

        bool IsUserInAllowList(User user);

        bool ShouldUserTransformIntoOrganization(User user);

        bool IsTenantIdPolicySupportedForOrganization(string emailAddress, string tenantId);

        bool IsEmailInExceptionsList(string emailAddress);

        bool AddEmailToExceptionsList(string emailAddress);

        bool RemoveEmailFromExceptionsList(string emailAddress);
    }
}
