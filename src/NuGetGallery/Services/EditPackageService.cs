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
        public virtual PackageEdit GetPendingMetadata(Package p)
        {
            return EntitiesContext.Set<PackageEdit>()
                .Where(m => m.PackageKey == p.Key)
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefault();
        }

        public virtual void StartEditPackageRequest(Package p, EditPackageVersionRequest request, User editingUser)
        {
            var edit = new PackageEdit
            {
                // No longer change these fields as they are no longer editable.
                // To be removed in a next wave of package immutability implementation.
                // (when no pending edits left to be processed in the db)
                Authors = p.FlattenedAuthors,
                Copyright = p.Copyright,
                Description = p.Description,
                IconUrl = p.IconUrl,
                LicenseUrl = p.LicenseUrl,
                ProjectUrl = p.ProjectUrl,
                ReleaseNotes = p.ReleaseNotes,
                RequiresLicenseAcceptance = p.RequiresLicenseAcceptance,
                Summary = p.Summary,
                Tags = p.Tags,
                Title = p.Title,
                
                // Readme
                ReadMeState = request.ReadMeState,

                // Other
                User = editingUser,
                Package = p,
                Timestamp = DateTime.UtcNow,
                TriedCount = 0,
            };

            EntitiesContext.Set<PackageEdit>().Add(edit);
            // Note: EditPackageRequests are completed asynchronously by the worker role.
        }
    }
}