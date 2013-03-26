using System.Data.Entity.Migrations;
using System.Linq;
using NuGetGallery.Data.Model;

namespace NuGetGallery.Data.Migrations
{
    public class MigrationsConfiguration : DbMigrationsConfiguration<EntitiesContext>
    {
        public MigrationsConfiguration()
        {
            AutomaticMigrationsEnabled = false;
        }
    }
}
