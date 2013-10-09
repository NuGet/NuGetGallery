using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks.DataManagement
{
    [Command("normalizepackageversions", "Sets the NormalizedVersion column for packages which do not have a value for that column", AltName = "npv")]
    public class NormalizePackageVersionsTask : DatabaseTask
    {
        private const string UpdateQuery = @"
            UPDATE Packages
            SET NormalizedVersion = ut.NormalizedVersion
            OUTPUT
                pr.Id,
                INSERTED.[Version],
                INSERTED.NormalizedVersion
            FROM @updateTable ut
                INNER JOIN Packages p ON ut.PackageKey = p.[Key]
                INNER JOIN PackageRegistrations pr ON p.PackageRegistrationKey = pr.[Key]";
        private const string CommitQuery = @"BEGIN TRAN
            " + UpdateQuery + @"
            COMMIT TRAN";
        private const string WhatIfQuery = @"BEGIN TRAN
            " + UpdateQuery + @"
            ROLLBACK TRAN";

        public override void ExecuteCommand()
        {
            WithConnection((c, db) =>
            {
                Log.Trace("Collecting list of packages...");
                var packages = db.Query<Package>(@"
                    SELECT pr.Id, p.[Key], p.Version
                    FROM Packages p
                        INNER JOIN PackageRegistrations pr ON pr.[Key] = p.PackageRegistrationKey
                    WHERE p.NormalizedVersion IS NULL")
                        .ToList();
                Log.Trace("Collected {0} packages", packages.Count);

                DataTable output;
                int count = 0;
                try
                {
                    // Create a table-type for the query
                    db.Execute(@"
                        IF EXISTS (
                            SELECT * 
                            FROM sys.types 
                            WHERE is_table_type = 1 
                            AND name = 'Temp_NormalizePackageVersionsInputType'
                        )
                        BEGIN
                            DROP TYPE Temp_NormalizePackageVersionsInputType
                        END
                        CREATE TYPE Temp_NormalizePackageVersionsInputType AS TABLE(PackageKey int, NormalizedVersion nvarchar(64))");

                    // Build a table to hold the new data
                    var updateTable = new DataTable();
                    updateTable.Columns.Add(new DataColumn("PackageKey", typeof(int)));
                    updateTable.Columns.Add(new DataColumn("NormalizedVersion", typeof(string)));
                    foreach (var package in packages)
                    {
                        string normalized = SemanticVersionExtensions.Normalize(package.Version);
                        var row = updateTable.NewRow();
                        row.SetField("PackageKey", package.Key);
                        row.SetField("NormalizedVersion", normalized);
                        updateTable.Rows.Add(row);
                    }

                    // Run the query with the table parameter
                    var cmd = c.CreateCommand();
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = WhatIf ? WhatIfQuery : CommitQuery;
                    cmd.Parameters.Add(new SqlParameter("@updateTable", SqlDbType.Structured)
                    {
                        TypeName = "Temp_NormalizePackageVersionsInputType",
                        Value = updateTable
                    });
                    Log.Trace("Updating Database...");
                    var reader = cmd.ExecuteReader();
                    Log.Trace("Database Update Complete");
                    
                    // Load the results into a datatable and render them
                    output = new DataTable();
                    output.Load(reader);
                    foreach (var row in output.Rows.Cast<DataRow>())
                    {
                        string id = row.Field<string>("Id");
                        string version = row.Field<string>("Version");
                        string normalized = row.Field<string>("NormalizedVersion");
                        if (!String.Equals(version, normalized, StringComparison.Ordinal))
                        {
                            count++;
                        }
                    }
                    Log.Info("Updated {0} packages", count);
                }
                finally
                {
                    // Clean up the type
                    db.Execute(@"
                        IF EXISTS (
                            SELECT * 
                            FROM sys.types 
                            WHERE is_table_type = 1 
                            AND name = 'Temp_NormalizePackageVersionsInputType'
                        ) DROP TYPE Temp_NormalizePackageVersionsInputType");
                }
            });
        }
    }
}
