using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public class PackageHeadingModel
    {
        public string PageHeading { get; }
        public string Id { get; }
        public string Version { get; }
        public bool ShowProfileBreadcrumb { get; }
        public string PackageDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(Version))
                {
                    return Id.Abbreviate(25);
                } else
                {
                    return $"{Id.Abbreviate(15)} {Version.Abbreviate(10)}";
                }
            }
        }
        public bool LinkIdOnly => string.IsNullOrEmpty(Version);

        public PackageHeadingModel(User currentUser, Package package, string pageHeading, bool linkIdOnly = false)
            : this(currentUser, package.PackageRegistration, pageHeading)
        {
            if (!linkIdOnly)
            {
                Version = package.Version;
            }
        }

        public PackageHeadingModel(User currentUser, PackageRegistration packageRegistration, string pageHeading)
            : this(packageRegistration.Id, pageHeading)
        {
            ShowProfileBreadcrumb = 
                ActionsRequiringPermissions.ShowProfileBreadcrumb.CheckPermissionsOnBehalfOfAnyAccount(currentUser, packageRegistration) == PermissionsCheckResult.Allowed;
        }

        public PackageHeadingModel(ListPackageItemViewModel packageViewModel, string pageHeading, bool linkIdOnly = false)
            : this(packageViewModel.Id, packageViewModel.Version, pageHeading)
        {
            ShowProfileBreadcrumb = packageViewModel.CanSeeBreadcrumbWithProfile;
        }

        public PackageHeadingModel(string id, string version, string pageHeading, bool linkIdOnly = false)
            : this(id, pageHeading)
        {
            if (!linkIdOnly)
            {
                Version = version;
            }
        }

        public PackageHeadingModel(string id, string pageHeading)
        {
            Id = id;
            PageHeading = pageHeading;
        }
    }
}