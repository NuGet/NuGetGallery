using System.Collections.Generic;

namespace NuGetGallery
{
    public class OwnerRequestsViewModel
    {
        public OwnerRequestsListViewModel Incoming { get; private set; }

        public OwnerRequestsListViewModel Outgoing { get; private set; }

        public OwnerRequestsViewModel(IEnumerable<PackageOwnerRequest> incoming, IEnumerable<PackageOwnerRequest> outgoing, User currentUser, IPackageService packageService)
            : this(new OwnerRequestsListViewModel(incoming, "Incoming", currentUser, packageService), new OwnerRequestsListViewModel(outgoing, "Outgoing", currentUser, packageService))
        {
        }

        public OwnerRequestsViewModel(OwnerRequestsListViewModel incoming, OwnerRequestsListViewModel outgoing)
        {
            Incoming = incoming;
            Outgoing = outgoing;
        }
    }
}