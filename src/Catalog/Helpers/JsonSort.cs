// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    /// <summary>
    /// This is a hacky attempt at organizing compacted json into a more visually appealing form.
    /// </summary>
    public class JsonSort : IComparer<JToken>
    {
        /// <summary>
        /// Order the json so arrays are at the bottom and single properties are at the top
        /// </summary>
        public static JObject OrderJson(JObject json)
        {
            JObject ordered = new JObject();

            var children = json.Children().ToList();

            children.Sort(new JsonSort());

            foreach (var child in children)
            {
                ordered.Add(child);
            }

            return ordered;
        }

        public int Compare(JToken x, JToken y)
        {
            JProperty xProp = x as JProperty;
            JProperty yProp = y as JProperty;

            if (xProp != null && yProp == null)
            {
                return -1;
            }

            if (xProp == null && yProp != null)
            {
                return 1;
            }

            if (xProp != null && yProp != null)
            {
                if (xProp.Name.Equals("@id"))
                {
                    return -1;
                }

                if (yProp.Name.Equals("@id"))
                {
                    return 1;
                }

                if (xProp.Name.Equals("@type"))
                {
                    return -1;
                }

                if (yProp.Name.Equals("@type"))
                {
                    return 1;
                }

                if (xProp.Name.Equals("@context"))
                {
                    return 1;
                }

                if (yProp.Name.Equals("@context"))
                {
                    return -1;
                }

                JArray xValArray = xProp.Value as JArray;
                JArray yValArray = yProp.Value as JArray;

                if (xValArray == null && yValArray != null)
                {
                    return -1;
                }

                if (xValArray != null && yValArray == null)
                {
                    return 1;
                }

                if (xProp.Name.StartsWith("@") && !yProp.Name.StartsWith("@"))
                {
                    return 1;
                }

                if (!xProp.Name.StartsWith("@") && yProp.Name.StartsWith("@"))
                {
                    return -1;
                }

                return StringComparer.OrdinalIgnoreCase.Compare(xProp.Name, yProp.Name);
            }

            return 0;
        }
    }
}
