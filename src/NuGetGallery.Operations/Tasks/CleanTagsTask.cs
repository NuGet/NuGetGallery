// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks
{
    [Command("cleantabs", "Cleans up Tags by removing commas", AltName = "ctabs", MaxArgs = 0)]
    public class CleanTagsTask : DatabaseTask
    {
        public override void ExecuteCommand()
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString.ConnectionString))
            {
                connection.Open();

                string sql = "UPDATE Packages SET Tags = REPLACE(REPLACE(Tags, ',', ' '), '  ', ' ')";

                SqlCommand command = new SqlCommand(sql, connection);
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 60 * 5;

                command.ExecuteNonQuery();
            }
        }
    }
}
