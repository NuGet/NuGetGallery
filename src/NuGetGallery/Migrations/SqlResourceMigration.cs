// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity.Migrations;
using System.IO;
using System.Text.RegularExpressions;

namespace NuGetGallery.Migrations
{
    public class SqlResourceMigration : DbMigration
    {
        /// <summary>
        /// Note that this delimiter is not perfect. It does not account for multi-line string literals in T-SQL. For
        /// now, the delimiter is sufficient.
        /// </summary>
        private static readonly Regex Go = new Regex(@"(\n|\r\n)GO;?(\n|\r\n)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        private readonly string _sqlFileUp;
        private readonly string _sqlFileDown;

        public SqlResourceMigration(string embeddedResourceSqlFile)
            : this(embeddedResourceSqlFile, embeddedResourceSqlFileDown: null)
        {
        }

        public SqlResourceMigration(string embeddedResourceSqlFileUp, string embeddedResourceSqlFileDown)
        {
            _sqlFileUp = embeddedResourceSqlFileUp;
            _sqlFileDown = embeddedResourceSqlFileDown;
        }

        public override void Up()
        {
            ExecuteSqlFile(_sqlFileUp);
        }

        public override void Down()
        {
            ExecuteSqlFile(_sqlFileDown);
        }

        private void ExecuteSqlFile(string sqlFile)
        {
            if (sqlFile == null)
            {
                return;
            }

            var stream = typeof(SqlResourceMigration).Assembly.GetManifestResourceStream(sqlFile);
            using (var streamReader = new StreamReader(stream))
            {
                var statements = Go.Split(streamReader.ReadToEnd());

                foreach (var statement in statements)
                {
                    if (String.IsNullOrWhiteSpace(statement))
                    {
                        continue;
                    }

                    Sql(statement);
                }
            }
        }
    }
}