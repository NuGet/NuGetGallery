﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks.CuratedFeeds
{
    [Command("listcuratedfeeds", "Lists all available curated feeds", AltName = "lcf")]
    public class ListCuratedFeedTask : DatabaseTask
    {
        public override void ExecuteCommand()
        {
            WithConnection((connection, db) =>
            {
                Log.Info("Curated Feeds:");
                foreach (var feed in db.Query<string>("SELECT Name FROM CuratedFeeds"))
                {
                    Log.Info("* {0}", feed);
                }
            });
        }
    }
}
