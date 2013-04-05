using System;

namespace NuGetGallery
{
    // Design notes:
    // IsFollowedFlag + LastModified timestamp for 'creating' and 'deleting' the follow relationship in a way we can query and batch process
    public class UserFollowsPackage : IEntity
    {
        public static UserFollowsPackage Create(int userKey, int packageRegistrationKey)
        {
            return new UserFollowsPackage
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

        public bool IsFollowed { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }
    }
}