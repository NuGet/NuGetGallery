using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace NuGetGallery.Framework
{
    public static class Fakes
    {
        public static readonly User User = new User("testUser");
        public static readonly User Admin = new User("testAdmin");
        public static readonly User Owner = new User("testPackageOwner") { EmailAddress = "confirmed@example.com" }; //package owners need confirmed email addresses, obviously.

        public static readonly PackageRegistration Package = new PackageRegistration()
        {
            Id = "FakePackage",
            Owners = new List<User>() { Owner },
            Packages = new List<Package>() {
                new Package() { Version = "1.0" },
                new Package() { Version = "2.0" }
            }
        };

        public static IPrincipal ToPrincipal(this User user)
        {
            return new GenericPrincipal(
                new GenericIdentity(user.Username),
                user.Roles == null ? new string[0] :
                user.Roles.Select(r => r.Name).ToArray());
        }

        public static IIdentity ToIdentity(this User user)
        {
             return new GenericIdentity(user.Username);
        }
    }
}
