using System;
using System.Data.Entity.Migrations;
using System.IO;

namespace NuGetGallery.Migrations
{
    public class StoredProcedureMigration : DbMigration
    {
        private string _sqlFile;
        private static readonly string[] Go = new[] { "GO" };

        public StoredProcedureMigration(string embeddedResourceSqlFile)
        {
            _sqlFile = embeddedResourceSqlFile;
        }

        public override void Up()
        {
            using (var stream = typeof(ExecuteELMAHSql).Assembly.GetManifestResourceStream(_sqlFile))
            {
                using (var streamReader = new StreamReader(stream))
                {
                    var statements = streamReader.ReadToEnd().Split(Go, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var statement in statements)
                    {
                        if (String.IsNullOrWhiteSpace(statement))
                        {
                            continue;
                        }

                        Sql(statement);
                    }
                }
            }
        }

        public override void Down()
        {
        }
    }
}