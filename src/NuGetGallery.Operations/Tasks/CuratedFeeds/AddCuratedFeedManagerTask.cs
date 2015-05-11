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
    [Command("addcuratedfeedmanager", "Adds an existing user as a manager to an existing feed", AltName = "acfm")]
    public class AddCuratedFeedManagerTask : DatabaseTask
    {
        [Option("The name of the feed", AltName = "f")]
        public string FeedName { get; set; }

        [Option("The name of the user", AltName = "u")]
        public string UserName { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            ArgCheck.Required(FeedName, "FeedName");
            ArgCheck.Required(UserName, "UserName");
        }

        public override void ExecuteCommand()
        {
            WithConnection((connection, db) =>
            {
                Log.Info("Adding {0} as manager of {1}", UserName, FeedName);

                string results = db.Query<string>(@"BEGIN TRAN
                    IF NOT EXISTS (
                        SELECT * 
                        FROM CuratedFeedManagers cfm 
                        JOIN Users u ON u.[Key] = cfm.UserKey
                        JOIN CuratedFeeds cf ON cf.[Key] = cfm.CuratedFeedKey
                        WHERE cf.Name = @FeedName
                        AND u.Username = @UserName
                    )
                        INSERT INTO CuratedFeedManagers(CuratedFeedKey, UserKey)
                            OUTPUT 'success'
                        SELECT 
                            (SELECT [Key] FROM CuratedFeeds WHERE Name = @FeedName) AS CuratedFeedKey,
                            (SELECT [Key] FROM Users WHERE Username = @UserName) AS UserKey
                    " + (WhatIf ? "ROLLBACK TRAN" : "COMMIT TRAN"),
                    new { FeedName, UserName }).FirstOrDefault();
                
                if (results == "success")
                {
                    Log.Info("Added {0} as manager of {1}", UserName, FeedName);
                }
                else
                {
                    Log.Warn("{0} is already a manager of {1}", UserName, FeedName);
                }
            });
        }
    }
}
