using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class OwnerRequestsListViewModel
    {
        public IEnumerable<OwnerRequestsListItemViewModel> RequestItems { get; private set; }

        public string Name { get; private set; }

        public User CurrentUser { get; private set; }
        
        public OwnerRequestsListViewModel(IEnumerable<PackageOwnerRequest> requests, string name, User currentUser, IPackageService packageService)
        {
            RequestItems = requests.Select(r => new OwnerRequestsListItemViewModel(r, packageService)).ToArray();
            Name = name;
            CurrentUser = currentUser;
        }
    }
}