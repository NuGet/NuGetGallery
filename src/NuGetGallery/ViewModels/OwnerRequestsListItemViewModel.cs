using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class OwnerRequestsListItemViewModel
    {
        public PackageOwnerRequest Request { get; private set; }

        public Package Package { get; private set; }

        public OwnerRequestsListItemViewModel(PackageOwnerRequest request, IPackageService packageService)
            : this(request, packageService.FindPackageByIdAndVersion(request.PackageRegistration.Id, null, SemVerLevelKey.SemVer2, true))
        {
        }

        public OwnerRequestsListItemViewModel(PackageOwnerRequest request, Package package)
        {
            Request = request;
            Package = package;
        }
    }
}