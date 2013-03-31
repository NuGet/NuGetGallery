using System.Data.SqlClient;
using AnglicanGeek.DbExecutor;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("deletefullpackage", "Delete all versions of the specified package", AltName = "dfp")]
    public class DeleteAllPackageVersionsTask : DatabaseAndStorageTask
    {
        [Option("The ID of the package", AltName = "p")]
        public string PackageId { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            ArgCheck.Required(PackageId, "PackageId");
        }

        public override void ExecuteCommand()
        {
            Log.Info(
                "Deleting package registration and all package versions for '{0}'.",
                PackageId);

            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();

                var packageRegistration = Util.GetPackageRegistration(
                    dbExecutor,
                    PackageId);
                var packages = Util.GetPackages(
                    dbExecutor,
                    packageRegistration.Key);
                
                foreach(var package in packages)
                {
                    new DeletePackageVersionTask {
                        ConnectionString = ConnectionString,
                        StorageAccount = StorageAccount,
                        PackageId = package.Id,
                        PackageVersion = package.Version,
                        WhatIf = WhatIf
                    }.ExecuteCommand();
                }

                Log.Info(
                    "Deleting package registration data for '{0}'",
                    packageRegistration.Id);
                if (!WhatIf)
                {
                    dbExecutor.Execute(
                        "DELETE por FROM PackageOwnerRequests por JOIN PackageRegistrations pr ON pr.[Key] = por.PackageRegistrationKey WHERE pr.[Key] = @packageRegistrationKey",
                        new { packageRegistrationKey = packageRegistration.Key });
                    dbExecutor.Execute(
                        "DELETE pro FROM PackageRegistrationOwners pro JOIN PackageRegistrations pr ON pr.[Key] = pro.PackageRegistrationKey WHERE pr.[Key] = @packageRegistrationKey",
                        new { packageRegistrationKey = packageRegistration.Key });
                    dbExecutor.Execute(
                        "DELETE FROM PackageRegistrations WHERE [Key] = @packageRegistrationKey",
                        new { packageRegistrationKey = packageRegistration.Key });
                }
            }

            Log.Info(
                "Deleted package registration and all package versions for '{0}'.",
                PackageId);
        }
    }
}
