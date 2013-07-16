using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace NuGetGallery
{
    public class EditPackageService
    {
        public IEntitiesContext EntitiesContext { get; set; }

        public EditPackageService()
        {
        }

        public virtual void StartEditPackageRequest(Package p, EditPackageRequest formData)
        {
            PackageMetadata edit = new PackageMetadata
            {
                // Description
                Authors = formData.EditPackageVersionRequest.Authors,
                Copyright = formData.EditPackageVersionRequest.Copyright,
                Description = formData.EditPackageVersionRequest.Description,
                IconUrl = formData.EditPackageVersionRequest.IconUrl,
                LicenseUrl = p.Metadata.LicenseUrl, // Our current policy is not to allow editing the license URL, so just clone it from its previous value.
                ProjectUrl = formData.EditPackageVersionRequest.ProjectUrl,
                ReleaseNotes = formData.EditPackageVersionRequest.ReleaseNotes,
                Summary = formData.EditPackageVersionRequest.Summary,
                Tags = formData.EditPackageVersionRequest.Tags,
                Title = formData.EditPackageVersionRequest.VersionTitle,

                // Other
                Package = p,
                PackageKey = p.Key,
                IsCompleted = false,
                Timestamp = DateTime.UtcNow,
                TriedCount = 0,
                EditName = "edit_" + EntitiesContext.Set<PackageMetadata>()
                    .Where(pe => pe.PackageKey == p.Key)
                    .Count()
                    .ToString(CultureInfo.InvariantCulture),
            };

            EntitiesContext.Set<PackageMetadata>().Add(edit);
            // Note: EditPackageRequests are completed asynchronously by the worker role.
        }
    }
}