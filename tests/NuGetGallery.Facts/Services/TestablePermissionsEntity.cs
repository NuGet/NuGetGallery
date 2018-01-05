using System.Collections.Generic;

namespace NuGetGallery
{
    public class TestablePermissionsEntity
    {
        public IEnumerable<User> Owners { get; }

        public TestablePermissionsEntity(IEnumerable<User> owners)
        {
            Owners = owners;
        }
    }
}
