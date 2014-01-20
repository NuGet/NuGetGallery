using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dapper
{
    public static class DapperExtensions
    {
        public static Task ExecuteAsync(this SqlConnection connection, string sql)
        {
            SqlCommand cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;
            return cmd.ExecuteNonQueryAsync();
        }
    }
}
