using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnglicanGeek.DbExecutor;

namespace NuGetGallery.Operations.Infrastructure
{
    public class SqlDbExecutorFactory : IDbExecutorFactory
    {
        public IDbExecutor OpenConnection(string connectionString)
        {
            return new SqlExecutor(new SqlConnection(connectionString));
        }
    }
}
