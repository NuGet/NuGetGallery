// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks.CuratedFeeds
{
    [Command("createcuratedfeed", "Creates a new, empty, curated feed", AltName = "ncf", MinArgs=1, MaxArgs=1)]
    public class CreateCuratedFeedTask : DatabaseTask
    {
        public override void ExecuteCommand()
        {
            WithConnection((connection, db) =>
            {
                Log.Info("Creating Curated Feed: {0}", Arguments[0]);

                string results;
                if(!WhatIf) {
                    results = db.Query<string>(@"
                        IF NOT EXISTS (SELECT * FROM CuratedFeeds WHERE Name = @Name)
                            INSERT INTO CuratedFeeds(Name)
                                OUTPUT inserted.Name
                            VALUES(@Name)",
                        new { Name = Arguments[0] }).FirstOrDefault();
                } else {
                    results = Arguments[0];
                }

                if (results != null)
                {
                    Log.Info("Created Curated Feed: {0}", results);
                }
                else
                {
                    Log.Warn("Curated Feed already exists: {0}", Arguments[0]);
                }
            });
        }
    }
}
