using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using AnglicanGeek.DbExecutor;

namespace NuGetGallery.Operations.Tasks
{
    [Command("comparepackageshash", "Given two databases in the same server, checks if the hash of the package versions are same in both", AltName = "cph")]
    public class SqlConnectionStringBuilder : DatabaseTask
    {
        [Option("Connection string to the destination database to be compared against", AltName = "dc")]
        public System.Data.SqlClient.SqlConnectionStringBuilder DestinationConnectionString { get; set; }

        public override void ExecuteCommand()
        { 
            //Establish connection to the source.
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    Log.Info("Connecting to the source...");
                    sqlConnection.Open();
                    //Get the list of packages.
                    var packages = dbExecutor.Query<Package>(
                                "SELECT p.[Key], pr.Id, p.Version, p.ExternalPackageUrl, p.Hash FROM Packages p JOIN PackageRegistrations pr ON pr.[Key] = p.PackageRegistrationKey ");

                    //Establish connection to the destination.
                    Log.Info("Connecting to the destination...");
                    using (var destSqlConnection = new SqlConnection(DestinationConnectionString.ConnectionString.ToString()))
                    {
                        using (var destDbExecutor = new SqlExecutor(destSqlConnection))
                        {
                            destSqlConnection.Open();
                            //Get the list of packages.
                            var destPackages = destDbExecutor.Query<Package>(
                                        "SELECT p.[Key], pr.Id, p.Version, p.ExternalPackageUrl, p.Hash FROM Packages p JOIN PackageRegistrations pr ON pr.[Key] = p.PackageRegistrationKey ");


                            int count = destPackages.Count();
                            int index = 0;
                            foreach (Package destp in destPackages)
                            {
                                index++;
                                var percentage = (int)(((double)index / (double)count) * 100);
                                //Check if the hash matches.
                                if(packages.Any(item => item.Key.Equals(destp.Key)))
                                {
                                    Log.Info(" [{1}%] Comparing package with key : {0}.", destp.Key, percentage);
                                    var package = packages.Where(item => item.Key.Equals(destp.Key)).ToList()[0];
                                    if (destp.Hash != package.Hash) //We can extend the check here to check for all immutable data instead of just the hash.
                                        Log.Error("Error:The hashes doesnt match for Package with Key : {0}, Id : {1}, Version : {2}. Source Hash : {3}, Dest Hash : {4}", package.Key, package.Id, package.Version, package.Hash, destp.Hash);
                                    else
                                        Log.Info("Hashes match in source and destination databaes. Source hash : {0}, Dest hash : {1}", package.Hash, destp.Hash);
                                }

                            }
                        }
                    }

                }
            }

        }

    }
}
