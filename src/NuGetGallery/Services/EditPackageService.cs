// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace NuGetGallery
{
    public class EditPackageService
    {
        public IEntitiesContext EntitiesContext { get; set; }

        public EditPackageService() { }

        public EditPackageService(IEntitiesContext entitiesContext)
        {
            EntitiesContext = entitiesContext;
        }

        /// <summary>
        /// Returns the newest, uncompleted metadata for a package (i.e. a pending edit)
        /// </summary>
        public virtual PackageEdit GetPendingMetadata(Package package)
        {
            return EntitiesContext.Set<PackageEdit>()
                .Where(m => m.PackageKey == package.Key)
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefault();
        }

        public virtual void StartEditPackageRequest(Package package, EditPackageVersionReadMeRequest request, User editingUser)
        {
            var edit = new PackageEdit
            {
                // No longer change these fields as they are no longer editable.
                // To be removed in a next wave of package immutability implementation.
                // (when no pending edits left to be processed in the db)
                Authors = package.FlattenedAuthors,
                Copyright = package.Copyright,
                Description = package.Description,
                IconUrl = package.IconUrl,
                LicenseUrl = package.LicenseUrl,
                ProjectUrl = package.ProjectUrl,
                ReleaseNotes = package.ReleaseNotes,
                RequiresLicenseAcceptance = package.RequiresLicenseAcceptance,
                Summary = package.Summary,
                Tags = package.Tags,
                Title = package.Title,
                
                // Readme
                ReadMeState = request.ReadMeState,

                // Other
                User = editingUser,
                Package = package,
                Timestamp = DateTime.UtcNow,
                TriedCount = 0,
            };

            EntitiesContext.Set<PackageEdit>().Add(edit);
            // Note: EditPackageRequests are completed asynchronously by the worker role.
        }
    }
}