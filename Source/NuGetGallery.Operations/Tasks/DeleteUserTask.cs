using System.Data.SqlClient;
using System.Linq;
using AnglicanGeek.DbExecutor;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations.Tasks
{
    [Command("deleteuser", "Delete a user's account and all of their packages", AltName = "du")]
    public class DeleteUserTask : DatabaseAndStorageTask
    {
        [Option("The username of the user to delete", AltName = "u")]
        public string Username { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            ArgCheck.Required(Username, "Username");
        }

        public override void ExecuteCommand()
        {
            Log.Info(
                "Delete the user account and all packages for '{0}'.",
                Username);

            using (var sqlConnection = new SqlConnection(ConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();

                var user = Util.GetUser(dbExecutor, Username);

                if (user == null)
                {
                    Log.Error("User was not found");
                    return;
                }

                var packageCount = user.PackageRegistrationIds.Count();

                Log.Info("User found with EmailAddress '{0}' and UnconfirmedEmailAddress '{1}'",
                    user.EmailAddress, user.UnconfirmedEmailAddress);

                foreach (var packageId in user.PackageRegistrationIds)
                {
                    Log.Info("User has {0} package registrations (as the sole owner) remaining to delete",
                        packageCount--);

                    var deletePackageTask = new DeleteAllPackageVersionsTask
                    {
                        ConnectionString = ConnectionString,
                        StorageAccount = StorageAccount,
                        PackageId = packageId,
                        WhatIf = WhatIf
                    };

                    deletePackageTask.Execute();
                }

                Log.Info("Deleting remaining package ownership records (from shared ownership)");

                if (!WhatIf)
                {
                    dbExecutor.Execute(
                        "DELETE pro FROM PackageRegistrationOwners pro WHERE pro.UserKey = @userKey",
                        new { userKey = user.Key });
                }

                Log.Info("Deleting package ownership requests");

                if (!WhatIf)
                {
                    dbExecutor.Execute(
                        "DELETE por FROM PackageOwnerRequests por WHERE @userKey IN (por.NewOwnerKey, por.RequestingOwnerKey)",
                        new { userKey = user.Key });
                }

                Log.Info("Deleting the user record itself");

                if (!WhatIf)
                {
                    dbExecutor.Execute(
                        "DELETE u FROM Users u WHERE u.[Key] = @userKey",
                        new { userKey = user.Key });
                }

                Log.Info(
                    "Deleted all packages owned solely by '{0}' as well as the user record."
                    , user.Username);
            }
        }
    }
}
