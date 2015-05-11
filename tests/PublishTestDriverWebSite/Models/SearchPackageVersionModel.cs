// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using PublishTestDriverWebSite.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PublishTestDriverWebSite.Models
{
    public class SearchPackageVersionModel
    {
        public SearchPackageVersionModel(JObject searchResultVersion)
        {
            Version = JsonUtils.Get(searchResultVersion, "version");
            Downloads = int.Parse(JsonUtils.Get(searchResultVersion, "downloads"));
        }

        public string Version { get; private set; }
        public int Downloads { get; private set; }
    }
}