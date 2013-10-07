using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Principal;

namespace NuGetGallery.Framework
{
    public static class Fakes
    {
        private static readonly MethodInfo SetMethod = typeof(IEntitiesContext).GetMethod("Set");

        public static readonly string Password = "p@ssw0rd!";

        public static readonly User User = new User("testUser") { Credentials = new List<Credential>() { CredentialBuilder.CreatePbkdf2Password(Password) } };
        public static readonly User Admin = new User("testAdmin") { Credentials = new List<Credential>() { CredentialBuilder.CreatePbkdf2Password(Password) } };
        public static readonly User Owner = new User("testPackageOwner") { Credentials = new List<Credential>() { CredentialBuilder.CreatePbkdf2Password(Password) }, EmailAddress = "confirmed@example.com" }; //package owners need confirmed email addresses, obviously.

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

        internal static void ConfigureEntitiesContext(FakeEntitiesContext ctxt)
        {
            var fields = typeof(Fakes)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => typeof(IEntity).IsAssignableFrom(f.FieldType));
            foreach (var field in fields)
            {
                object set = SetMethod.MakeGenericMethod(field.FieldType).Invoke(ctxt, new object[0]);
                var method = set.GetType().GetMethod("Add");
                method.Invoke(set, new object[] { field.GetValue(null) });
            }
        }
    }
}
