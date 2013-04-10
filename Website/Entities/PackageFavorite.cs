using System;

namespace NuGetGallery
{
    // Design notes:
    // IsFavorited flag + LastModified timestamp for 'creating' and 'deleting' the favorite relationship in a way we can batch process
    public class PackageFavorite : IEntity
    {
        public static PackageFavorite Create(int userKey, int packageRegistrationKey)
        {
            return new PackageFavorite
            {
                UserKey = userKey,
                PackageRegistrationKey = packageRegistrationKey,
                Created = DateTime.UtcNow,
            };
        }

        public int Key { get; set; }

        public int UserKey { get; set; }
        public User User { get; set; }

        public int PackageRegistrationKey { get; set; }
        public PackageRegistration PackageRegistration { get; set; }

        public bool IsFavorited { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }
    }
}