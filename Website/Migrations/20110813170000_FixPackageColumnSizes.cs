using System.Data;
using Migrator.Framework;

namespace NuGetGallery.Data.Migrations {
    [Migration(20110813170000)]
    public class FixPackageColumnSizes : Migration {
        public override void Up() {
            Database.ExecuteNonQuery(
@"CREATE PROCEDURE DropUniqueConstraint
    @table nvarchar(255),
    @column nvarchar(255)
AS
    DECLARE @indices CURSOR
   
    SET @indices = CURSOR FOR
        select sysindexes.name from sysindexkeys,syscolumns,sysindexes
            WHERE
                syscolumns.[id] = OBJECT_ID(@table)
                AND sysindexkeys.[id] = OBJECT_ID(@table)
                AND sysindexes.[id] = OBJECT_ID(@table)
                AND syscolumns.name=@column
                AND sysindexkeys.colid=syscolumns.colid
                AND sysindexes.[indid]=sysindexkeys.[indid]
                AND (
                    SELECT COUNT(*) FROM sysindexkeys AS si2
                    WHERE si2.id=sysindexes.id
                    AND si2.indid=sysindexes.indid
                )=1
    OPEN @indices
    DECLARE @index Nvarchar(255)
    FETCH NEXT FROM @indices INTO @index

    WHILE @@FETCH_STATUS = 0 BEGIN
        DECLARE @dropSql nvarchar(4000)

        SET @dropSql=
            N'ALTER TABLE [' + @table + N']
                DROP CONSTRAINT [' + @index + N']'
        EXEC(@dropSql)
           
        FETCH NEXT FROM @indices
        INTO @index
    END
CLOSE @indices
DEALLOCATE @indices");

            Database.ExecuteNonQuery("EXEC DropUniqueConstraint @table='PackageRegistrations', @column='Id'");
            Database.ExecuteNonQuery("ALTER TABLE PackageRegistrations ALTER COLUMN Id nvarchar(128)");
            Database.AddUniqueConstraint("UQ_PackageRegistrations_Id", "PackageRegistrations", "Id");

            Database.RemoveConstraint("Packages", "UQ_Packages_KeyAndVersion");
            Database.ExecuteNonQuery("ALTER TABLE Packages ALTER COLUMN Version nvarchar(128)");
            Database.AddUniqueConstraint("UQ_Packages_KeyAndVersion", "Packages", new[] { "[Key]", "Version" });
            Database.ExecuteNonQuery("ALTER TABLE Packages ALTER COLUMN Copyright nvarchar(4000)");
            Database.ExecuteNonQuery("ALTER TABLE Packages ALTER COLUMN Description nvarchar(4000)");
            Database.ExecuteNonQuery("ALTER TABLE Packages ALTER COLUMN ExternalPackageUrl nvarchar(4000)");
            Database.ExecuteNonQuery("ALTER TABLE Packages ALTER COLUMN FlattenedAuthors nvarchar(4000)");
            Database.ExecuteNonQuery("ALTER TABLE Packages ALTER COLUMN FlattenedDependencies nvarchar(4000)");
            Database.ExecuteNonQuery("ALTER TABLE Packages ALTER COLUMN Hash nvarchar(4000)");
            Database.ExecuteNonQuery("ALTER TABLE Packages ALTER COLUMN HashAlgorithm nvarchar(4000)");
            Database.ExecuteNonQuery("ALTER TABLE Packages ALTER COLUMN IconUrl nvarchar(4000)");
            Database.ExecuteNonQuery("ALTER TABLE Packages ALTER COLUMN LicenseUrl nvarchar(4000)");
            Database.ExecuteNonQuery("ALTER TABLE Packages ALTER COLUMN ProjectUrl nvarchar(4000)");
            Database.ExecuteNonQuery("ALTER TABLE Packages ALTER COLUMN Summary nvarchar(4000)");
            Database.ExecuteNonQuery("ALTER TABLE Packages ALTER COLUMN Tags nvarchar(4000)");
            Database.ExecuteNonQuery("ALTER TABLE Packages ALTER COLUMN Title nvarchar(4000)");
        }

        public override void Down() {
            // No need to revert to the old column sizes
            Database.ExecuteNonQuery("DROP PROCEDURE DropUniqueConstraint");
        }
    }
}