// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks.CuratedFeeds
{
    [Command("listcuratedfeedmanagers", "Lists the managers for a curated feed", AltName = "lcfm", MinArgs = 1, MaxArgs = 1)]
    public class ListCuratedFeedManagersTask : DatabaseTask
    {
        public override void ExecuteCommand()
        {
            WithConnection((connection, db) =>
            {
                Log.Info("Managers of {0}:", Arguments[0]);
                var results = db.Query<string>(@"
                    SELECT u.Username
                    FROM CuratedFeedManagers cfm
                    JOIN CuratedFeeds cf ON cfm.CuratedFeedKey = cf.[Key]
                    JOIN Users u ON cfm.UserKey = u.[Key]
                    WHERE cf.Name = @Name",
                    new { Name = Arguments[0] });
                foreach (var manager in results)
                {
                    Log.Info("* {0}", manager);
                }
            });
        }
    }
}
