// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Data.SqlClient;

namespace NuGetGallery.Operations.Common
{
    static class SqlHelper
    {
        public static void ExecuteBatch(string connectionString, string sql, int timeout = 180)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    SqlCommand command = new SqlCommand(sql, conn);
                    command.CommandTimeout = timeout;
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                throw new ApplicationException($"{e.Message}\n{sql}", e);
            }
        }
    }
}
