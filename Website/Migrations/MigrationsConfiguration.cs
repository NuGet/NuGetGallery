using System;
using System.Data.Entity.Migrations;
using System.Linq;

namespace NuGetGallery.Migrations
{
    public class MigrationsConfiguration : DbMigrationsConfiguration<EntitiesContext>
    {
        public MigrationsConfiguration()
        {
            AutomaticMigrationsEnabled = false;
        }

        protected override void Seed(EntitiesContext context)
        {
            var roles = context.Set<Role>();
            if (!roles.Any(x => x.Name == Constants.AdminRoleName))
            {
                roles.Add(new Role { Name = Constants.AdminRoleName });
                context.SaveChanges();
            }

            var users = context.Set<User>();
            if (!users.Any(x => x.Username == Constants.SystemUserName))
            {
                // @SYSTEM is just a user with a special (invalid) username, used to denote things not related to a real logged in user 
                // - it shouldn't have any special privileges but it helps distinguish which Metadata objects were automatically created.
                var apiKey = Guid.NewGuid(); 
                var hash = CryptographyService.GenerateSaltedHash(apiKey.ToString(), Constants.PBKDF2HashAlgorithmId);
                var user = new User
                {
                    CreatedUtc = DateTime.UtcNow,
                    Username = Constants.SystemUserName,
                    ApiKey = apiKey, // So nobody can push packages as @SYSTEM (unless they got DB access...)
                    HashedPassword = hash, // So nobody can log in as @SYSTEM (unless they got DB access...)
                    PasswordHashAlgorithm = Constants.PBKDF2HashAlgorithmId,
                };

                users.Add(user);
                context.SaveChanges();
            }
        }
    }
}
