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
    [Command("removecuratedfeedmanager", "Removes an existing manager from a curated feed", AltName = "rcfm")]
    public class RemoveCuratedFeedManagerTask : DatabaseTask
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
                Log.Info("Removing {0} from {1}", UserName, FeedName);

                int results = db.Execute(@"BEGIN TRAN
                    DELETE CuratedFeedManagers
                    FROM CuratedFeedManagers AS cfm 
                    JOIN Users u ON u.[Key] = cfm.UserKey
                    JOIN CuratedFeeds cf ON cf.[Key] = cfm.CuratedFeedKey
                    WHERE cf.Name = @FeedName
                    AND u.Username = @UserName
                    " + (WhatIf ? "ROLLBACK TRAN" : "COMMIT TRAN"),
                    new { FeedName, UserName });
                
                if (results > 0)
                {
                    Log.Info("Removed {0} from {1}", UserName, FeedName);
                }
                else
                {
                    Log.Warn("{0} is not a manager of {1}", UserName, FeedName);
                }
            });
        }
    }
}
