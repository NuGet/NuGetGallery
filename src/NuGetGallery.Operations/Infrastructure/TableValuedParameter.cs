// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Dapper;
using Microsoft.SqlServer.Server;

namespace NuGetGallery.Operations.Infrastructure
{
    public class TableValuedParameter : SqlMapper.IDynamicParameters
    {
        public string Name { get; private set; }
        public string TableType { get; private set; }
        public DataTable TableValue { get; private set; }

        public TableValuedParameter(string name, string tableType, DataTable tableValue)
        {
            Name = name;
            TableType = tableType;
            TableValue = tableValue;
        }

        public void AddParameters(IDbCommand command, SqlMapper.Identity identity)
        {
            var sqlCommand = (SqlCommand)command;
            var param = new SqlParameter(Name, TableValue)
            {
                TypeName = TableType,
                SqlDbType = SqlDbType.Structured,
                Direction = ParameterDirection.Input
            };
            sqlCommand.Parameters.Add(param);
        }
    }
}
