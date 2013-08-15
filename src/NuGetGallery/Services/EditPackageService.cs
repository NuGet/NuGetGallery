using System;
using System.Data.Entity;
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
                .Where(m => m.PackageKey == p.Key)
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefault();
        }

        public virtual void StartEditPackageRequest(Package p, EditPackageVersionRequest request, User editingUser)
        {
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