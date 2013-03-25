using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using NuGetGallery.Data;
using NuGetGallery.Data.Model;
using Xunit;

namespace NuGetGallery.Entities
{
    class EntitiesContextFacts
    {
        // We allow use of the EntitiesContext ctor here because we are testing it :)
#pragma warning disable 618
        [Fact]
        public void SaveChangesFailsInReadOnlyMode()
        {
            var modelBuilder = new DbModelBuilder();
            modelBuilder.Entity<User>();
            var ec = new EntitiesContext("",
                                         modelBuilder.Build(new DbProviderInfo("System.Data.SqlClient", "2008"))
                                             .Compile(),
                                         readOnly: true);
            Assert.Throws<ReadOnlyException>(() => ec.Users.Add(new User
            {
                Key = -1,
                Username = "FredFredFred",
            }));
        }
#pragma warning restore 618
    }
}
