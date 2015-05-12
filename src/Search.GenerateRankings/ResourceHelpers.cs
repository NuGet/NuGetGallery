using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Search.GenerateRankings.Helpers
{
    public static class ResourceHelpers
    {
        public static Task<string> ReadResourceFile(string name)
        {
            return ReadResourceFile(name, typeof(ResourceHelpers).Assembly);
        }

        public static async Task<string> ReadResourceFile(string resourceName, Assembly asm)
        {
            string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(assemblyName + "." + resourceName))
            using (var reader = new StreamReader(stream))
            {
                return await reader.ReadToEndAsync();
            }
        }
    }
}
