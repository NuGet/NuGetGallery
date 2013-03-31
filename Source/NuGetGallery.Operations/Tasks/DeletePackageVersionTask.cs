using System.Data.SqlClient;
using AnglicanGeek.DbExecutor;

namespace NuGetGallery.Operations
{
    [Command("deletepackageversion", "Delete a specific package version", AltName = "dpv")]
    public class DeletePackageVersionTask : DatabasePackageVersionTask
    {
        public override void ExecuteCommand()
        {
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();

                var package = Util.GetPackage(
                    dbExecutor,
                    PackageId,
                    PackageVersion);

                if (package == null)
                {
                    Log.Error("Package version does not exist: '{0}.{1}'", PackageId, PackageVersion);
                    return;
                }

                Log.Info(
                    "Deleting package data for '{0}.{1}'", 
                    package.Id, 
                    package.Version);

                if (!WhatIf)
                {
                    dbExecutor.Execute(
                        "DELETE pa FROM PackageAuthors pa JOIN Packages p ON p.[Key] = pa.PackageKey WHERE p.[Key] = @key",
                        new { key = package.Key });
                    dbExecutor.Execute(
                        "DELETE pd FROM PackageDependencies pd JOIN Packages p ON p.[Key] = pd.PackageKey WHERE p.[Key] = @key",
                        new { key = package.Key });
                    dbExecutor.Execute(
                        "DELETE ps FROM PackageStatistics ps JOIN Packages p ON p.[Key] = ps.PackageKey WHERE p.[Key] = @key",
                        new { key = package.Key });
                    dbExecutor.Execute(
                        "DELETE pf FROM PackageFrameworks pf JOIN Packages p ON p.[Key] = pf.Package_Key WHERE p.[Key] = @key",
                        new { key = package.Key });
                    dbExecutor.Execute(
                        "DELETE p FROM Packages p JOIN PackageRegistrations pr ON pr.[Key] = p.PackageRegistrationKey WHERE p.[Key] = @key",
                        new { key = package.Key });
                }

                new DeletePackageFileTask {
                    StorageAccount = StorageAccount,
                    PackageId = package.Id,
                    PackageVersion = package.Version,
                    PackageHash = package.Hash,
                    WhatIf = WhatIf
                }.ExecuteCommand();
            }
        }
    }
}
