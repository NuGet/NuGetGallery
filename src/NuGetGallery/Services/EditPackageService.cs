using System;
using System.Diagnostics;
using System.Globalization;
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
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefault(
                    m => m.PackageKey == p.Key
                );
        }

        public virtual void StartEditPackageRequest(Package p, EditPackageRequest formData, User editingUser)
        {
            PackageEdit edit = new PackageEdit
            {
                // Description
                User = editingUser,
                Authors = formData.EditPackageVersionRequest.Authors,
                Copyright = formData.EditPackageVersionRequest.Copyright,
                Description = formData.EditPackageVersionRequest.Description,
                IconUrl = formData.EditPackageVersionRequest.IconUrl,
                LicenseUrl = p.LicenseUrl, // Our current policy is not to allow editing the license URL, so just clone it from its previous value.
                ProjectUrl = formData.EditPackageVersionRequest.ProjectUrl,
                ReleaseNotes = formData.EditPackageVersionRequest.ReleaseNotes,
                RequiresLicenseAcceptance = formData.EditPackageVersionRequest.RequiresLicenseAcceptance,
                Summary = formData.EditPackageVersionRequest.Summary,
                Tags = formData.EditPackageVersionRequest.Tags,
                Title = formData.EditPackageVersionRequest.VersionTitle,

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