using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Framework
{
    public static class Fakes
    {
        public static readonly FakeUser User = new FakeUser("testUser");
        public static readonly FakeUser Admin = new FakeUser("testAdmin", Constants.AdminRoleName);
        public static readonly FakeUser Owner = new FakeUser("testPackageOwner");
        public static readonly PackageRegistration Package = new PackageRegistration()
        {
            Id = "FakePackage",
            Owners = new List<User>() { Owner.User },
            Packages = new List<Package>() {
                new Package() { Version = "1.0" },
                new Package() { Version = "2.0" }
            }
        };

        public class FakeUser {
            public string UserName { get; private set; }
            public IIdentity Identity { get; private set; }
            public IPrincipal Principal { get; private set; }
            public User User { get; private set; }

            public FakeUser(string userName, params string[] roles) {
                UserName = userName;
                Identity = new GenericIdentity(UserName);
                Principal = new GenericPrincipal(Identity, roles);
                User = new User()
                {
                    Username = UserName
                };
            }
        }
    }
}
