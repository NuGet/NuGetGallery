// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace NuGetGallery
{
    public class EditPackageService
    {
        public IEntitiesContext EntitiesContext { get; set; }
        public IPackageNamingConflictValidator PackageNamingConflictValidator { get; set; }

        public EditPackageService() { }

        public EditPackageService(
            IEntitiesContext entitiesContext,
            IPackageNamingConflictValidator packageNamingConflictValidator)
        {
            EntitiesContext = entitiesContext;
            PackageNamingConflictValidator = packageNamingConflictValidator;
        }

        /// <summary>
        /// Returns the newest, uncompleted metadata for a package (i.e. a pending edit)
        /// </summary>
        public virtual PackageEdit GetPendingMetadata(Package p)
        {
            return EntitiesContext.Set<PackageEdit>()
                .Where(m => m.PackageKey == p.Key)
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefault();
        }

        public virtual void StartEditPackageRequest(Package p, EditPackageVersionRequest request, User editingUser)
        {
            if (PackageNamingConflictValidator.TitleConflictsWithExistingRegistrationId(p.PackageRegistration.Id, request.VersionTitle))
            {
                throw new EntityException(Strings.TitleMatchesExistingRegistration, request.VersionTitle);
            }

            PackageEdit edit = new PackageEdit
            {
                // Description
                User = editingUser,
                Authors = request.Authors,
                Copyright = request.Copyright,
                Description = request.Description,
                IconUrl = request.IconUrl,
                LicenseUrl = p.LicenseUrl, // Our current policy is not to allow editing the license URL, so just clone it from its previous value.
                ProjectUrl = request.ProjectUrl,
                ReleaseNotes = request.ReleaseNotes,
                RequiresLicenseAcceptance = request.RequiresLicenseAcceptance,
                Summary = request.Summary,
                Tags = request.Tags,
                Title = request.VersionTitle,

                // Other
                Package = p,
                Timestamp = DateTime.UtcNow,
                TriedCount = 0,
            };

            EntitiesContext.Set<PackageEdit>().Add(edit);
            // Note: EditPackageRequests are completed asynchronously by the worker role.
        }
    }
}