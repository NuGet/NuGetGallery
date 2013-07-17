using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks
{
    [Command("updatelicensereports", "Updates the license reports from SonaType", AltName="ulr")]
    public class UpdateLicenseReportsTask : DatabaseTask
    {
        private static readonly string[] _licenses = new[] {
            "Apache 2 (TEST)",
            "MIT (TEST)",
            "BSD (TEST)",
            "GPL (TEST), BSD (TEST)",
            "EPL (TEST), MIT (TEST), BSD (TEST)",
            "Apache 2 (TEST), Just do what you want (TEST)",
            null
        };

        public override void ExecuteCommand()
        {
            // Get all packages!
            WithConnection((c, db) =>
            {
                // Grab the top 100 most downloaded packages
                var packages = db.Query<Package>(@"
                    SELECT TOP 100 r.Id, (SELECT TOP 1 [Version] FROM Packages WHERE PackageRegistrationKey = r.[Key] AND IsLatestStable = 1) AS 'Version', (SELECT TOP 1 [Key] FROM Packages WHERE PackageRegistrationKey = r.[Key] AND IsLatestStable = 1) AS 'Key'
                    FROM Packages p
                    INNER JOIN PackageRegistrations r ON p.PackageRegistrationKey = r.[Key]
                    WHERE EXISTS (SELECT * FROM Packages WHERE PackageRegistrationKey = r.[Key] AND IsLatestStable = 1)
                    GROUP BY r.Id, r.[Key]
                    ORDER BY MAX(r.DownloadCount) DESC").ToList();

                var r = new Random();
                foreach (var package in packages)
                {
                    // Pick a random license set for it
                    var licenses = _licenses[r.Next(_licenses.Length - 1)];

                    // Flip a coin to determine if we put a URL in
                    var reportUrl = (r.Next(100) % 3 == 0) ? "http://www.microsoft.com" : null;

                    // Add the data!
                    Log.Info("Adding '{0}' and '{1}' to '{2}@{3}'", licenses, reportUrl, package.Id, package.Version);
                    if (!WhatIf)
                    {
                        db.Execute(@"
                            UPDATE Packages 
                            SET LicenseNames = @licenses, LicenseReportUrl = @reportUrl
                            WHERE [Key] = @key",
                            new { licenses, reportUrl, key = package.Key });
                    }
                }
            });
        }
    }
}
