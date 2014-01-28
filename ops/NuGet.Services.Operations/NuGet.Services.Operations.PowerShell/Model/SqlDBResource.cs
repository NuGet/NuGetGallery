using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace NuGet.Services.Operations.Model
{
    public class SqlDBResource : Resource
    {
        public static readonly string ElementName = "sqldb";

        public SqlConnectionStringBuilder ConnectionString { get; set; }
    }
}
