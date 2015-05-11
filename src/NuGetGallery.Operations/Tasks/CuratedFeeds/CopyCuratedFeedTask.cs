// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations.Tasks.CuratedFeeds
{
    [Command("copycuratedfeed", "Copies all the content of a curated feed to a new feed with a different name", AltName="ccf")]
    public class CopyCuratedFeedTask : DatabaseTask
    {
        // Queries
        private const string UpdateManagersBaseQuery = @"
	        DECLARE @newId int
	        SELECT @newId = [Key] FROM CuratedFeeds WHERE Name = @DestinationFeed

	        DECLARE @results TABLE (
		        [Action] nvarchar(10),
		        CuratedFeedKey int,
		        UserKey int
	        )

	        MERGE INTO CuratedFeedManagers AS t
		        USING (
			        SELECT @newId, cfm.UserKey
			        FROM CuratedFeedManagers cfm
			        JOIN CuratedFeeds cf ON cfm.CuratedFeedKey = cf.[Key]
			        WHERE cf.Name = @SourceFeed) AS s(CuratedFeedKey, UserKey)
		        ON (s.CuratedFeedKey = t.CuratedFeedKey AND s.UserKey = t.UserKey)
		        WHEN NOT MATCHED BY TARGET 
			        THEN INSERT(CuratedFeedKey, UserKey) VALUES(s.CuratedFeedKey, s.UserKey)
		        OUTPUT $action, inserted.CuratedFeedKey, inserted.UserKey INTO @results;

	        SELECT r.*, cf.Name, u.Username
	        FROM @results r
	        JOIN CuratedFeeds cf ON cf.[Key] = r.CuratedFeedKey
	        JOIN Users u ON u.[Key] = r.UserKey";
        private const string UpdateManagersWhatIfQuery = @"BEGIN TRAN
" + UpdateManagersBaseQuery + @"
ROLLBACK TRAN";
        private const string UpdateManagersRealQuery = @"BEGIN TRAN
" + UpdateManagersBaseQuery + @"
COMMIT TRAN";

        private const string UpdatePackagesBaseQuery = @"
            DECLARE @newId int
	        SELECT @newId = [Key] FROM CuratedFeeds WHERE Name = @DestinationFeed

	        DECLARE @results TABLE (
		        [Action] nvarchar(10),
		        CuratedFeedKey int,
		        PackageRegistrationKey int
	        )

	        MERGE INTO CuratedPackages AS t
		        USING (
			        SELECT @newId, cp.Notes, cp.PackageRegistrationKey, cp.AutomaticallyCurated, cp.Included
			        FROM CuratedPackages cp
			        JOIN CuratedFeeds cf ON cp.CuratedFeedKey = cf.[Key]
			        WHERE cf.Name = @SourceFeed) AS s(CuratedFeedKey, Notes, PackageRegistrationKey, AutomaticallyCurated, Included)
		        ON (s.CuratedFeedKey = t.CuratedFeedKey AND s.PackageRegistrationKey = t.PackageRegistrationKey)
		        WHEN NOT MATCHED BY TARGET 
			        THEN INSERT(CuratedFeedKey, Notes, PackageRegistrationKey, AutomaticallyCurated, Included) VALUES(s.CuratedFeedKey, s.Notes, s.PackageRegistrationKey, s.AutomaticallyCurated, s.Included)
		        OUTPUT $action AS [Action], (CASE $action 
			        WHEN 'DELETE' THEN deleted.CuratedFeedKey
			        ELSE inserted.CuratedFeedKey 
		        END) AS CuratedFeedKey, (CASE $action 
			        WHEN 'DELETE' THEN deleted.PackageRegistrationKey
			        ELSE inserted.PackageRegistrationKey 
		        END) AS PackageRegistrationKey INTO @results;

		        SELECT r.*, pr.Id, cf.Name 
		        FROM @results r 
		        JOIN PackageRegistrations pr ON r.[PackageRegistrationKey] = pr.[Key]
		        JOIN CuratedFeeds cf ON r.CuratedFeedKey = cf.[Key]";
        private const string UpdatePackagesWhatIfQuery = @"BEGIN TRAN
" + UpdatePackagesBaseQuery + @"
ROLLBACK TRAN";
        private const string UpdatePackagesRealQuery = @"BEGIN TRAN
" + UpdatePackagesBaseQuery + @"
COMMIT TRAN";


        [Option("The name of the source feed", AltName = "s")]
        public string SourceFeed { get; set; }

        [Option("The name of the destination feed", AltName = "d")]
        public string DestinationFeed { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            ArgCheck.Required(SourceFeed, "SourceFeed");
            ArgCheck.Required(DestinationFeed, "DestinationFeed");
        }

        public override void ExecuteCommand()
        {
            WithConnection((connection, db) =>
            {
                // Check for the source feed
                int count = db.Execute(
                    "SELECT COUNT(*) FROM CuratedFeeds WHERE Name = @name",
                    new { name = SourceFeed });
                if (count == 0)
                {
                    Log.Error("Source Feed '{0}' does not exist", SourceFeed);
                    return;
                }

                // Check for the destination feed
                Log.Info("Ensuring Feed '{0}' exists", DestinationFeed);
                db.Execute(@"
                    IF NOT EXISTS (SELECT * FROM CuratedFeeds WHERE Name = @name)
                        INSERT INTO CuratedFeeds(Name) VALUES(@name)",
                    new { name = DestinationFeed });
                
                // Update Managers
                var param = new { SourceFeed, DestinationFeed };
                Log.Info("Updating Managers");
                var managerResults = db.Query<UpdateManagerResult>(
                    WhatIf ? UpdateManagersWhatIfQuery : UpdateManagersRealQuery,
                    param);
                foreach (var managerResult in managerResults)
                {
                    Log.Info("{2} {0} {1}", managerResult.Action, managerResult.Username, managerResult.Name);
                }

                // Update Packages
                Log.Info("Updating Packages");
                var packageResults = db.Query<UpdatePackageResult>(
                    WhatIf ? UpdatePackagesWhatIfQuery : UpdatePackagesRealQuery,
                    param);
                foreach (var packageResult in packageResults)
                {
                    Log.Info("{2} {0} {1}", packageResult.Action, packageResult.Id, packageResult.Name);
                }
            });
        }

        public class UpdateManagerResult
        {
            public string Action { get; set; }
            public int CuratedFeedKey { get; set; }
            public int UserKey { get; set; }
            public string Username { get; set; }
            public string Name { get; set; }
        }

        public class UpdatePackageResult
        {
            public string Action { get; set; }
            public int CuratedFeedKey { get; set; }
            public int PackageRegistrationKey { get; set; }
            public string Id { get; set; }
            public string Name { get; set; }
        }
    }
}
