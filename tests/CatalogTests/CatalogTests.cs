// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTests
{
    public class CatalogTests
    {
        public async Task Test0Async()
        {
            DateTime lastReadTime = DateTime.Parse("5/28/2014 9:04:10 PM");

            //string baseAddress = "http://linked.blob.core.windows.net/demo/";
            string baseAddress = "http://localhost:8000/test/";

            Uri address = new Uri(string.Format("{0}catalog/index.json", baseAddress));

            HttpClient client = new HttpClient();

            string indexJson = await client.GetStringAsync(address);
            JObject indexObj = JObject.Parse(indexJson);

            foreach (JToken indexItem in indexObj["items"])
            {
                DateTime indexItemTimeStamp = indexItem["commitTimestamp"].ToObject<DateTime>();

                if (indexItemTimeStamp > lastReadTime)
                {
                    string pageJson = await client.GetStringAsync(indexItem["url"].ToObject<Uri>());
                    JObject pageObj = JObject.Parse(pageJson);

                    foreach (JToken pageItem in pageObj["items"])
                    {
                        DateTime pageItemTimeStamp = pageItem["commitTimestamp"].ToObject<DateTime>();

                        if (pageItemTimeStamp > lastReadTime)
                        {

                            if (pageItem["@type"].ToString() == "http://tempuri.org/type#Drummer")
                            {
                                string dataJson = await client.GetStringAsync(pageItem["url"].ToObject<Uri>());
                                JObject dataObj = JObject.Parse(dataJson);

                                Console.WriteLine(dataObj["name"]);
                            }
                        }
                    }
                }
            }
        }

        public void Test0()
        {
            Test0Async().Wait();
        }
    }
}
