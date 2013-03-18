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

        public static Stream ToJson(Tuple<string[], List<string[]>> report)
        {
            JArray jArray = new JArray();

            foreach (string[] row in report.Item2)
            {
                JObject jObject = new JObject();

                for (int i = 0; i < report.Item1.Length; i++)
                {
                    jObject.Add(report.Item1[i], row[i]);
                }

                jArray.Add(jObject);
            }

            return ToStream(jArray);
        }
    }
}
