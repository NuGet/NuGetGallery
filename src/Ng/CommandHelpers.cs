using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ng
{
    static class CommandHelpers
    {
        public static IDictionary<string, string> GetArguments(string[] args, int start)
        {
            Console.WriteLine(args.Length);

            IDictionary<string, string> result = new Dictionary<string, string>();

            if (args.Length == start)
            {
                return result;
            }

            if ((args.Length - 1) % 2 != 0)
            {
                Trace.TraceError("Unexpected number of arguments");
                return null;
            }

            for (int i = 1; i < args.Length; i += 2)
            {
                result.Add(args[i], args[i + 1]);
            }

            return result;
        }

        static void TraceRequiredArgument(string name)
        {
            Trace.TraceError("Required argument \"{0}\" not provided", name);
        }

        public static string GetSource(IDictionary<string, string> arguments)
        {
            string source;
            if (!arguments.TryGetValue("-source", out source))
            {
                TraceRequiredArgument("-source");
                return null;
            }
            return source;
        }

        public static Storage CreateStorage(IDictionary<string, string> arguments)
        {
            string storageType;
            if (!arguments.TryGetValue("-storageType", out storageType))
            {
                TraceRequiredArgument("-storageType");
                return null;
            }

            string storageVerboseStr = "false";
            arguments.TryGetValue("-storageVerbose", out storageVerboseStr);

            bool storageVerbose = storageVerboseStr == null ? false : storageVerboseStr.Equals("true", StringComparison.InvariantCultureIgnoreCase);

            if (storageType.Equals("File", StringComparison.InvariantCultureIgnoreCase))
            {
                string storageBaseAddress;
                if (!arguments.TryGetValue("-storageBaseAddress", out storageBaseAddress))
                {
                    TraceRequiredArgument("-storageBaseAddress");
                    return null;
                }

                string storagePath;
                if (!arguments.TryGetValue("-storagePath", out storagePath))
                {
                    TraceRequiredArgument("-storagePath");
                    return null;
                }

                return new FileStorage(storageBaseAddress, storagePath) { Verbose = storageVerbose };
            }
            else if (storageType.Equals("Azure", StringComparison.InvariantCultureIgnoreCase))
            {
                string storageAccountName;
                if (!arguments.TryGetValue("-storageAccountName", out storageAccountName))
                {
                    TraceRequiredArgument("-storageAccountName");
                    return null;
                }

                string storageKeyValue;
                if (!arguments.TryGetValue("-storageKeyValue", out storageKeyValue))
                {
                    TraceRequiredArgument("-storageKeyValue");
                    return null;
                }

                string storageContainer;
                if (!arguments.TryGetValue("-storageContainer", out storageContainer))
                {
                    TraceRequiredArgument("-storageContainer");
                    return null;
                }

                string storagePath;
                if (!arguments.TryGetValue("-storagePath", out storagePath))
                {
                    TraceRequiredArgument("-storagePath");
                    return null;
                }

                StorageCredentials credentials = new StorageCredentials(storageAccountName, storageKeyValue);
                CloudStorageAccount account = new CloudStorageAccount(credentials, true);
                return new AzureStorage(account, storageContainer, storagePath) { Verbose = storageVerbose };
            }
            else
            {
                return null;
            }
        }
    }
}
