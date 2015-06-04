// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetFeed
{
    public class InterceptStep : BuildStep
    {

        public InterceptStep(Config config)
            : base(config, "CreateIntercept")
        {

        }

        protected override void RunCore(CancellationToken cancellationToken)
        {
            FileInfo file = new FileInfo(Path.Combine(Config.Catalog.RootFolder.FullName, "intercept.json"));

            if (file.Exists)
            {
                file.Delete();
            }

            using (JsonTextWriter writer = new JsonTextWriter(File.CreateText(file.FullName)))
            {
                writer.Formatting = Formatting.Indented;

                writer.WriteStartObject();

                writer.WritePropertyName("resolverBaseAddress");
                writer.WriteValue(Config.PackageRegistrations.BaseAddress.AbsoluteUri);

                writer.WritePropertyName("isLatest");
                writer.WriteValue("");

                writer.WritePropertyName("isLatestStable");
                writer.WriteValue("");

                writer.WritePropertyName("allVersions");
                writer.WriteValue("");

                writer.WritePropertyName("searchAddress");
                writer.WriteValue("");

                writer.WritePropertyName("passThroughAddress");
                writer.WriteValue(Config.Catalog.EndpointAddress);

                writer.WriteEndObject();
            }
        }
    }
}
