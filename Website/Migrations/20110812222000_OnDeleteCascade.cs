using Migrator.Framework;

namespace NuGetGallery.Data.Migrations {
    [Migration(20110812222000)]
    public class OnDeleteCascadeMigration : Migration {
        public override void Up() {
            Database.RemoveForeignKey("Packages", "FK_Packages_PackageRegistrations");
            Database.RemoveForeignKey("PackageRegistrationOwners", "FK_PackageRegistrationOwners_PackageRegistrations");
            Database.RemoveForeignKey("PackageRegistrationOwners", "FK_PackageRegistrationOwners_Users");
            Database.RemoveForeignKey("PackageAuthors", "FK_PackageAuthors_Packages");
            Database.RemoveForeignKey("PackageDependencies", "FK_PackageDependencies_Packages");
            Database.AddForeignKey("FK_Packages_PackageRegistrations", "Packages", "PackageRegistrationKey", "PackageRegistrations", "[Key]", Migrator.Framework.ForeignKeyConstraint.Cascade);
            Database.AddForeignKey("FK_PackageRegistrationOwners_PackageRegistrations", "PackageRegistrationOwners", "PackageRegistrationKey", "PackageRegistrations", "[Key]", Migrator.Framework.ForeignKeyConstraint.Cascade);
            Database.AddForeignKey("FK_PackageRegistrationOwners_Users", "PackageRegistrationOwners", "UserKey", "Users", "[Key]", Migrator.Framework.ForeignKeyConstraint.Cascade);
            Database.AddForeignKey("FK_PackageAuthors_Packages", "PackageAuthors", "PackageKey", "Packages", "[Key]", Migrator.Framework.ForeignKeyConstraint.Cascade);
            Database.AddForeignKey("FK_PackageDependencies_Packages", "PackageDependencies", "PackageKey", "Packages", "[Key]", Migrator.Framework.ForeignKeyConstraint.Cascade);
        }

        public override void Down() {
            Database.RemoveForeignKey("Packages", "FK_Packages_PackageRegistrations");
            Database.RemoveForeignKey("PackageRegistrationOwners", "FK_PackageRegistrationOwners_PackageRegistrations");
            Database.RemoveForeignKey("PackageRegistrationOwners", "FK_PackageRegistrationOwners_Users");
            Database.RemoveForeignKey("PackageAuthors", "FK_PackageAuthors_Packages");
            Database.RemoveForeignKey("PackageDependencies", "FK_PackageDependencies_Packages");
            Database.AddForeignKey("FK_Packages_PackageRegistrations", "Packages", "PackageRegistrationKey", "PackageRegistrations", "[Key]");
            Database.AddForeignKey("FK_PackageRegistrationOwners_PackageRegistrations", "PackageRegistrationOwners", "PackageRegistrationKey", "PackageRegistrations", "[Key]");
            Database.AddForeignKey("FK_PackageRegistrationOwners_Users", "PackageRegistrationOwners", "UserKey", "Users", "[Key]");
            Database.AddForeignKey("FK_PackageAuthors_Packages", "PackageAuthors", "PackageKey", "Packages", "[Key]");
            Database.AddForeignKey("FK_PackageDependencies_Packages", "PackageDependencies", "PackageKey", "Packages", "[Key]");
        }
    }
}