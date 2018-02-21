// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class SortingIdCollector : SortingCollector<string>
    {
        public SortingIdCollector(Uri index, ITelemetryService telemetryService, Func<HttpMessageHandler> handlerFunc = null) : base(index, telemetryService, handlerFunc)
        {
        }

        protected override string GetKey(JObject item)
        {
            return item["nuget:id"].ToString();
        }
    }
}
