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
                throw new ApplicationException(string.Format("{0}\n{1}", e.Message, sql), e);
            }
        }
    }
}
