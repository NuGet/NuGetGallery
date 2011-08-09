using System;
using System.Linq;
using NuGet.Server.DataServices;

namespace NuGet.Server.Infrastructure {
    public interface IServerPackageRepository : IPackageRepository, ISearchableRepository {
        void RemovePackage(string packageId, Version version);
        Package GetMetadataPackage(IPackage package);
    }
}
