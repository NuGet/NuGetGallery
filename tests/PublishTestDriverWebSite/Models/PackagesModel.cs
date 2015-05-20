// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace PublishTestDriverWebSite.Models
{
    public class PackagesModel
    {
        public PackagesModel(JObject searchResults)
        {
            Packages = new List<SearchPackageModel>();

            foreach (JObject searchResult in searchResults["data"])
            {
                Packages.Add(new SearchPackageModel(searchResult));
            }
        }

        public List<SearchPackageModel> Packages { get; private set; }
    }
}