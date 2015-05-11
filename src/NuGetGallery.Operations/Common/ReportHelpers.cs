// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace NuGetGallery.Operations.Common
{
    static class ReportHelpers
    {
        public static Stream ToStream(JToken jToken)
        {
            MemoryStream stream = new MemoryStream();
            TextWriter writer = new StreamWriter(stream);
            writer.Write(jToken.ToString());
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        public static Stream ToJson(Tuple<string[], List<object[]>> report)
        {
            JArray jArray = new JArray();

            foreach (object[] row in report.Item2)
            {
                JObject jObject = new JObject();

                for (int i = 0; i < report.Item1.Length; i++)
                {
                    if (row[i] != null)
                    {
                        jObject.Add(report.Item1[i], new JValue(row[i]));
                    }
                    // ELSE treat null by not defining the property in our internal JSON (aka undefined)
                }

                jArray.Add(jObject);
            }

            return ToStream(jArray);
        }
    }
}
