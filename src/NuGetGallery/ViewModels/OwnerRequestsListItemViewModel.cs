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
        {
            Request = request;
            Package = packageService.FindPackageByIdAndVersion(request.PackageRegistration.Id, null, SemVerLevelKey.SemVer2, true);
        }
    }
}