// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.WarehouseIntegration;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CatalogTests
{
    class WarehouseCatalogTests
    {
        public static void Test0()
        {
            Storage storage = new FileStorage("http://localhost:8000/demo", @"c:\data\site\demo");

            SqlConnectionStringBuilder connStrBldr = new SqlConnectionStringBuilder();
            connStrBldr.IntegratedSecurity = true;
            connStrBldr.InitialCatalog = "TestSourceWarehouse";
            connStrBldr.DataSource = @"(LocalDB)\v11.0";

            string connectionString = connStrBldr.ToString();

            WarehouseHelper.CreateStatisticsCatalogAsync(storage, connectionString, CancellationToken.None).Wait();
        }

        public static void Test1()
        {
            Func<HttpMessageHandler> handlerFunc = () =>
            {
                return new FileSystemEmulatorHandler
                {
                    BaseAddress = new Uri("http://localhost:8000"),
                    RootFolder = @"c:\data\site",
                    InnerHandler = new HttpClientHandler()
                };
            };

            DateTime minDownloadTimeStamp = DateTime.Parse("2014-07-20");
            //DateTime minDownloadTimeStamp = DateTime.MinValue;

            StatsCountCollector collector = new StatsGreaterThanCountCollector(new Uri("http://localhost:8000/stats/index.json"), minDownloadTimeStamp, handlerFunc);
            //StatsCountCollector collector = new StatsLessThanCountCollector(new Uri("http://localhost:8000/stats/index.json"), minDownloadTimeStamp, handlerFunc);

            collector.Run(CancellationToken.None).Wait();

            Console.WriteLine("count = {0}", collector.Count);
        }
    }
}
