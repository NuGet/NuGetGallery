using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

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
            string value;
            if (!arguments.TryGetValue("-source", out value))
            {
                TraceRequiredArgument("-source");
                return null;
            }
            return value;
        }

        public static string GetRegistration(IDictionary<string, string> arguments)
        {
            string value;
            if (!arguments.TryGetValue("-registration", out value))
            {
                TraceRequiredArgument("-registration");
                return null;
            }
            return value;
        }

        public static string GetContentBaseAddress(IDictionary<string, string> arguments)
        {
            string value;
            if (!arguments.TryGetValue("-contentBaseAddress", out value))
            {
                TraceRequiredArgument("-contentBaseAddress");
                return null;
            }
            return value;
        }

        public static StorageFactory CreateStorageFactory(IDictionary<string, string> arguments)
        {
            string storageBaseAddress;
            if (!arguments.TryGetValue("-storageBaseAddress", out storageBaseAddress))
            {
                TraceRequiredArgument("-storageBaseAddress");
                return null;
            }

            string storageVerboseStr = "false";
            arguments.TryGetValue("-storageVerbose", out storageVerboseStr);

            bool storageVerbose = storageVerboseStr == null ? false : storageVerboseStr.Equals("true", StringComparison.InvariantCultureIgnoreCase);

            string storageType;
            if (!arguments.TryGetValue("-storageType", out storageType))
            {
                TraceRequiredArgument("-storageType");
                return null;
            }

            if (storageType.Equals("File", StringComparison.InvariantCultureIgnoreCase))
            {
                string storagePath;
                if (!arguments.TryGetValue("-storagePath", out storagePath))
                {
                    TraceRequiredArgument("-storagePath");
                    return null;
                }

                return new FileStorageFactory(new Uri(storageBaseAddress), storagePath) { Verbose = storageVerbose };
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
                return new AzureStorageFactory(account, storageContainer, storagePath, new Uri(storageBaseAddress)) { Verbose = storageVerbose };
            }
            else
            {
                Trace.TraceError("Unrecognized storageType \"{0}\"", storageType);
                return null;
            }
        }

        public static Lucene.Net.Store.Directory GetLuceneDirectory(IDictionary<string, string> arguments)
        {
            string luceneDirectoryType;
            if (!arguments.TryGetValue("-luceneDirectoryType", out luceneDirectoryType))
            {
                TraceRequiredArgument("-luceneDirectoryType");
                return null;
            }

            if (luceneDirectoryType.Equals("File", StringComparison.InvariantCultureIgnoreCase))
            {
                string lucenePath;
                if (!arguments.TryGetValue("-lucenePath", out lucenePath))
                {
                    TraceRequiredArgument("-lucenePath");
                    return null;
                }

                DirectoryInfo directoryInfo = new DirectoryInfo(lucenePath);

                return new SimpleFSDirectory(directoryInfo);
            }
            else if (luceneDirectoryType.Equals("Azure", StringComparison.InvariantCultureIgnoreCase))
            {
                string luceneStorageAccountName;
                if (!arguments.TryGetValue("-luceneStorageAccountName", out luceneStorageAccountName))
                {
                    TraceRequiredArgument("-luceneStorageAccountName");
                    return null;
                }

                string luceneStorageKeyValue;
                if (!arguments.TryGetValue("-luceneStorageKeyValue", out luceneStorageKeyValue))
                {
                    TraceRequiredArgument("-luceneStorageKeyValue");
                    return null;
                }

                string luceneStorageContainer;
                if (!arguments.TryGetValue("-luceneStorageContainer", out luceneStorageContainer))
                {
                    TraceRequiredArgument("-luceneStorageContainer");
                    return null;
                }

                StorageCredentials credentials = new StorageCredentials(luceneStorageAccountName, luceneStorageKeyValue);
                CloudStorageAccount account = new CloudStorageAccount(credentials, true);
                return new AzureDirectory(account, luceneStorageContainer, new RAMDirectory());
            }
            else
            {
                Trace.TraceError("Unrecognized luceneDirectoryType \"{0}\"", luceneDirectoryType);
                return null;
            }
        }
    }
}
