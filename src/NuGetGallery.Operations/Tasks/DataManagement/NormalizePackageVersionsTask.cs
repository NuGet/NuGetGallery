using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks.DataManagement
{
    public class NormalizePackageVersionsTask : DatabaseTask
    {
        private const string UpdateQuery = @"
            UPDATE Packages
            SET NormalizedVersion = ut.NormalizedVersion
            OUTPUT
                (SELECT pr.Id FROM PackageRegistrations pr WHERE pr.[Key] = PackageRegistrationKey) AS 'Id',
                Version,
                Packages.NormalizedVersion
            FROM @updateTable ut
                INNER JOIN Packages ON ut.PackageKey = [Key]";
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
                    SELECT pr.Id, p.Version, p.NormalizedVersion
                    FROM Packages p
                        INNER JOIN PackageRegistrations pr ON pr.[Key] = p.PackageRegistrationKey
                    WHERE p.NormalizedVersion IS NULL")
                    .ToList();
                Log.Trace("Collected {0} packages", packages.Count);

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
                cmd.Parameters.AddWithValue("@updateTable", updateTable);
                var reader = cmd.ExecuteReader();

                // Load the results into a datatable and render them
                var table = new DataTable();
                table.Load(reader);
                foreach (var row in table.Rows.Cast<DataRow>())
                {
                    string id = row.Field<string>("Id");
                    string version = row.Field<string>("Version");
                    string normalized = row.Field<string>("NormalizedVersion");
                    if (!String.Equals(version, normalized, StringComparison.Ordinal))
                    {
                        Log.Info("{0} {1} => {2}", id, version, normalized);
                    }
                }
            });
        }
    }
}
