using System;

namespace NuGetGallery
{
    // Design notes:
    // IsFollowedFlag + LastModified timestamp for 'creating' and 'deleting' the follow relationship in a way we can query and batch process
    public class UserFollowsPackage : IEntity
    {
        public static UserFollowsPackage Create(User user, PackageRegistration package)
        {
            return new UserFollowsPackage
            {
                User = user,
                PackageRegistration = package,
                Created = DateTime.UtcNow,
            };
        }

        public int Key { get; set; }

        public int UserKey { get; set; }
        public User User { get; set; }

        public int PackageRegistrationKey { get; set; }
        public PackageRegistration PackageRegistration { get; set; }

        public bool IsFollowed { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }
    }
}