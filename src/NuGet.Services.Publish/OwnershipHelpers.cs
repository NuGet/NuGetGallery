// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Services.Metadata.Catalog.Ownership;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NuGet.Services.Publish
{
    public static class OwnershipHelpers
    {
        public static async Task<IList<string>> CheckRegistrationAuthorization(IRegistrationOwnership registrationOwnership, PackageIdentity packageIdentity)
        {
            IList<string> errors = new List<string>();

            if (!await registrationOwnership.HasNamespace(packageIdentity.Namespace))
            {
                errors.Add("user is not allowed to publish in this namespace");
                return errors;
            }

            if (await registrationOwnership.HasRegistration(packageIdentity.Namespace, packageIdentity.Id))
            {
                if (!await registrationOwnership.HasOwner(packageIdentity.Namespace, packageIdentity.Id))
                {
                    errors.Add("user does not have access to this registration");
                    return errors;
                }

                if (await registrationOwnership.HasVersion(packageIdentity.Namespace, packageIdentity.Id, packageIdentity.Version.ToString()))
                {
                    errors.Add("this package version already exists for this registration");
                    return errors;
                }
            }

            return errors;
        }

        public static async Task<IList<string>> CheckRegistrationAuthorizationForEdit(IRegistrationOwnership registrationOwnership, PackageIdentity packageIdentity)
        {
            IList<string> errors = new List<string>();

            if (!await registrationOwnership.HasNamespace(packageIdentity.Namespace))
            {
                errors.Add("user is not allowed to publish in this namespace");
                return errors;
            }

            if (await registrationOwnership.HasRegistration(packageIdentity.Namespace, packageIdentity.Id))
            {
                if (!await registrationOwnership.HasOwner(packageIdentity.Namespace, packageIdentity.Id))
                {
                    errors.Add("user does not have access to this registration");
                    return errors;
                }
            }
            else
            {
                errors.Add("this package does not exist in the ownership record");
            }

            return errors;
        }

        public static async Task<PublicationDetails> CreatePublicationDetails(IRegistrationOwnership registrationOwnership, PublicationVisibility publicationVisibility)
        {
            string userId = registrationOwnership.GetUserId();
            string userName = await registrationOwnership.GetPublisherName();
            string tenantId = registrationOwnership.GetTenantId();

            //TODO: requires Graph access
            string tenantName = string.Empty;
            //string tenantName = await _registrationOwnership.GetTenantName();

            //var client = await ServiceHelpers.GetActiveDirectoryClient();

            PublicationDetails publicationDetails = new PublicationDetails
            {
                Published = DateTime.UtcNow,
                Owner = OwnershipOwner.Create(ClaimsPrincipal.Current),
                TenantId = tenantId,
                TenantName = tenantName,
                Visibility = publicationVisibility
            };

            return publicationDetails;
        }
    }
}