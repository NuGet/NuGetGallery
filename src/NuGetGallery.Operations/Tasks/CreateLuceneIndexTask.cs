using NuGetGallery.Operations.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Threading;

namespace NuGetGallery.Operations
{
    [Command("createluceneindex", "Create the Lucene Index from the Gallery database", IsSpecialPurpose = false)]
    public class CreateLuceneIndexTask : DatabaseTask
    {
        public override void ExecuteCommand()
        {
            Log.Info("Create Lucene Index begin");

            Log.Info("Create Lucene Index end");
        }
    }
}
