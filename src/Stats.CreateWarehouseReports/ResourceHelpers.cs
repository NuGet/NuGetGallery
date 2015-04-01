using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stats.CreateWarehouseReports.Helpers
{
    public static class ResourceHelpers
    {
        public static Task<string> ReadResourceFile(string name)
        {
            return ReadResourceFile(name, typeof(ResourceHelpers).Assembly);
        }

        public static async Task<string> ReadResourceFile(string name, Assembly asm)
        {
            using (var stream = asm.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream))
            {
                return await reader.ReadToEndAsync();
            }
        }
    }
}
