using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NuGet.Services.Operations.Model;

namespace NuGet.Services.Operations.Serialization
{
    public class SqlDBResourceParser : IParser<Resource>
    {
        public Resource Parse(XElement element)
        {
            return new SqlDBResource()
            {
                Name = element.AttributeValue("name"),
                Type = SqlDBResource.ElementName,
                ConnectionString = element.ValueAs<SqlConnectionStringBuilder>(s => new SqlConnectionStringBuilder(s))
            };
        }
    }
}
