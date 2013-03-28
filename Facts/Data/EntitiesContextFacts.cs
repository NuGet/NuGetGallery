using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using Xunit;

namespace NuGetGallery.Data
{
    class EntitiesContextFacts
    {
#pragma warning disable 618
        // Can't get the test to run now that we provide a model... EF complains that nameOrConnectionString must be non-null & non-empty.
        //[Fact]
        //public void SaveChangesFailsInReadOnlyMode()
        //{
        //    var modelBuilder = new DbModelBuilder();
        //    DbModelFactory.ConfigureModel(modelBuilder);
        //    var ec = new EntitiesContext("",
        //                                 modelBuilder.Build(new DbProviderInfo("System.Data.SqlClient", "2008"))
        //                                     .Compile(),
        //                                 readOnly: true);
        //    Assert.Throws<ReadOnlyException>(() => ec.Users.Add(new User
        //    {
        //        Key = -1,
        //        Username = "FredFredFred",
        //    }));
        //}
#pragma warning restore 618
    }
}
