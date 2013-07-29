using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using NuGet;

namespace NuGetGallery.Operations
{
    public static class ExtensionMethods
    {
        public static void AddRange<T>(this ICollection<T> self, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                self.Add(item);
            }
        }

        public static bool AnySafe<T>(this IEnumerable<T> items, Func<T, bool> predicate)
        {
            if (items == null)
            {
                return false;
            }
            return items.Any(predicate);
        }
        
        public static string ToShortNameOrNull(this FrameworkName frameworkName)
        {
            return frameworkName == null ? null : VersionUtility.GetShortFrameworkName(frameworkName);
        }

        public static string ToFriendlyDateTimeString(this DateTime self)
        {
            return self.ToString("yyyy-MM-dd h:mm tt");
        }

        public static T ReadJsonBlob<T>(this CloudBlobClient self, string containerName, string path)
        {
            var container = self.GetContainerReference(containerName);
            container.CreateIfNotExists();

            var blob = container.GetBlockBlobReference(path);
            if (blob.Exists())
            {
                using(var strm = blob.OpenRead())
                using (var sr = new StreamReader(strm))
                {
                    return JsonConvert.DeserializeObject<T>(sr.ReadToEnd());
                }
            }
            else
            {
                return default(T);
            }
        }

        public static void WriteJsonBlob<T>(this CloudBlobClient self, string containerName, string path, T value)
        {
            var container = self.GetContainerReference(containerName);
            container.CreateIfNotExists();

            var blob = container.GetBlockBlobReference(path);
            using (var strm = blob.OpenWrite())
            using (var sw = new StreamWriter(strm))
            {
                sw.Write(JsonConvert.SerializeObject(value));
            }
        }
    }
}
