// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Test
{
    public class FindFirstCollector : BatchCollector
    {
        string _id;
        string _version;

        public FindFirstCollector(Uri index, string id, string version = null, Func<HttpMessageHandler> handlerFunc = null, int batchSize = 200)
            : base(index, handlerFunc, batchSize)
        {
            _id = id;
            _version = (version != null) ? NuGetVersion.Parse(version).ToNormalizedString() : null;
        }

        public JObject PackageDetails { get; private set; }

        protected override async Task<bool> OnProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {
            foreach (JObject item in items)
            {
                string id = item["nuget:id"].ToString();
                string version = item["nuget:version"].ToString();

                //Console.WriteLine("{0}/{1}", id, version);

                if (_version != null)
                {
                    if (id.Equals(_id, StringComparison.InvariantCultureIgnoreCase) && version.Equals(_version, StringComparison.InvariantCultureIgnoreCase))
                    {
                        PackageDetails = await client.GetJObjectAsync(new Uri(item["@id"].ToString()));
                        return false;
                    }
                }
                else
                {
                    if (id.Equals(_id, StringComparison.InvariantCultureIgnoreCase))
                    {
                        PackageDetails = await client.GetJObjectAsync(new Uri(item["@id"].ToString()));
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
