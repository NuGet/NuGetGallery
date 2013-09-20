using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring
{
    public static class SqlConnectionExtensions
    {
        public static object ExecuteScalar(this SqlConnection self, string query)
        {
            SqlCommand cmd = self.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = query;
            return cmd.ExecuteScalar();
        }
    }
}
