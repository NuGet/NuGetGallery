using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace NuGetGallery
{
    public class ManagePackageOwnersViewModel : ListPackageItemViewModel
    {
        public ManagePackageOwnersViewModel(Package package, IPrincipal currentUser)
            : base(package)
        {
            CurrentOwnerUsername = currentUser.Identity.Name;
            OtherOwners = Owners.Where(o => o.Username != CurrentOwnerUsername);
        }

        public bool HasOtherOwners
        {
            get { return OtherOwners.Any(); }
        }

        public string CurrentOwnerUsername { get; private set; }
        public IEnumerable<User> OtherOwners { get; private set; }
    }
}