// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks.CuratedFeeds
{
    [Command("deletecuratedfeed", "Deletes ALL data for a curated feed", AltName = "dcf", MinArgs = 1, MaxArgs = 1)]
    public class DeleteCuratedFeedTask : DatabaseTask
    {
        public override void ExecuteCommand()
        {
            WithConnection((connection, db) =>
            {
                Log.Info("Deleting Curated Packages in: {0}", Arguments[0]);
                int results = db.Execute(@"BEGIN TRAN
                    DELETE FROM CuratedPackages
                    WHERE [CuratedFeedKey] = (SELECT [Key] FROM CuratedFeeds WHERE Name = @Name)
                    " + (WhatIf ? "ROLLBACK TRAN" : "COMMIT TRAN"),
                    new { Name = Arguments[0] });
                Log.Info("Deleted {0} packages", results);
                
                Log.Info("Deleting Curated Feed Managers for: {0}", Arguments[0]);
                results = db.Execute(@"BEGIN TRAN
                    DELETE FROM CuratedFeedManagers
                    WHERE [CuratedFeedKey] = (SELECT [Key] FROM CuratedFeeds WHERE Name = @Name)
                    " + (WhatIf ? "ROLLBACK TRAN" : "COMMIT TRAN"),
                    new { Name = Arguments[0] });
                Log.Info("Deleted {0} managers", results);

                Log.Info("Deleting Curated Feed: {0}", Arguments[0]);
                results = db.Execute(@"BEGIN TRAN
                    DELETE FROM CuratedFeeds
                    WHERE Name = @Name
                    " + (WhatIf ? "ROLLBACK TRAN" : "COMMIT TRAN"),
                    new { Name = Arguments[0] });
                Log.Info("Deleted {0} feeds", results);
            });
        }
    }
}
