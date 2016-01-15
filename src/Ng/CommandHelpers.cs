// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace Ng
{
    static class CommandHelpers
    {
        public static IDictionary<string, string> GetArguments(string[] args, int start)
        {
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

        private static void TraceRequiredArgument(string name)
        {
            Console.WriteLine("Required argument \"{0}\" not provided", name);
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

        public static string GetGallery(IDictionary<string, string> arguments)
        {
            string value;
            if (!arguments.TryGetValue("-gallery", out value))
            {
                return null;
            }
            return value;
        }

        public static string GetRegistration(IDictionary<string, string> arguments)
        {
            string value;
            if (!arguments.TryGetValue("-registration", out value))
            {
                return null;
            }
            return value;
        }

        public static string GetCatalogBaseAddress(IDictionary<string, string> arguments)
        {
            string value;
            if (!arguments.TryGetValue("-catalogBaseAddress", out value))
            {
                return null;
            }
            return value;
        }

        public static string GetStorageBaseAddress(IDictionary<string, string> arguments)
        {
            string value;
            if (!arguments.TryGetValue("-storageBaseAddress", out value))
            {
                return null;
            }
            return value;
        }

        public static string GetId(IDictionary<string, string> arguments)
        {
            string value;
            if (!arguments.TryGetValue("-id", out value))
            {
                return null;
            }
            return value;
        }

        public static string GetVersion(IDictionary<string, string> arguments)
        {
            string value;
            if (!arguments.TryGetValue("-version", out value))
            {
                return null;
            }
            return value;
        }

        public static bool GetUnlistShouldDelete(IDictionary<string, string> arguments)
        {
            string unlistShouldDeleteStr = null;
            if (arguments.TryGetValue("-unlistShouldDelete", out unlistShouldDeleteStr))
            {
                return unlistShouldDeleteStr.Equals("true", StringComparison.InvariantCultureIgnoreCase);
            }

            return false;
        }

        public static bool GetVerbose(IDictionary<string, string> arguments)
        {
            string verboseStr = "false";
            arguments.TryGetValue("-verbose", out verboseStr);

            bool verbose = verboseStr == null ? false : verboseStr.Equals("true", StringComparison.InvariantCultureIgnoreCase);

            return verbose;
        }

        public static int GetInterval(IDictionary<string, string> arguments)
        {
            const int DefaultInterval = 3; // seconds
            int interval = DefaultInterval;
            string intervalStr = string.Empty;
            if (arguments.TryGetValue("-interval", out intervalStr))
            {
                if (!int.TryParse(intervalStr, out interval))
                {
                    interval = DefaultInterval;
                }
            }
            return interval;
        }

        public static DateTime GetStartDate(IDictionary<string, string> arguments)
        {
            DateTime defaultStartDate = DateTime.MinValue;
            string startDateString;
            if (arguments.TryGetValue("-startDate", out startDateString))
            {
                if (!DateTime.TryParse(startDateString, out defaultStartDate))
                {
                    defaultStartDate = DateTime.MinValue;
                }
            }
            return defaultStartDate;
        }

        public static string GetLuceneRegistrationTemplate(IDictionary<string, string> arguments)
        {
            string value;
            if (!arguments.TryGetValue("-luceneRegistrationTemplate", out value))
            {
                TraceRequiredArgument("-luceneRegistrationTemplate");
                return null;
            }
            return value;
        }

        public static string GetContentBaseAddress(IDictionary<string, string> arguments)
        {
            string value;
            if (!arguments.TryGetValue("-contentBaseAddress", out value))
            {
                return null;
            }
            return value;
        }

        public static StorageFactory CreateStorageFactory(IDictionary<string, string> arguments, bool verbose)
        {
            Uri storageBaseAddress = null;
            string storageBaseAddressStr;
            if (arguments.TryGetValue("-storageBaseAddress", out storageBaseAddressStr))
            {
                storageBaseAddressStr = storageBaseAddressStr.TrimEnd('/') + "/";

                storageBaseAddress = new Uri(storageBaseAddressStr);
            }

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

                if (storageBaseAddress == null)
                {
                    TraceRequiredArgument("-storageBaseAddress");
                    return null;
                }

                return new FileStorageFactory(storageBaseAddress, storagePath) { Verbose = verbose };
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

                string storagePath = null;
                arguments.TryGetValue("-storagePath", out storagePath);

                StorageCredentials credentials = new StorageCredentials(storageAccountName, storageKeyValue);
                CloudStorageAccount account = new CloudStorageAccount(credentials, true);
                return new AzureStorageFactory(account, storageContainer, storagePath, storageBaseAddress) { Verbose = verbose };
            }
            else
            {
                Trace.TraceError("Unrecognized storageType \"{0}\"", storageType);
                return null;
            }
        }

        public static StorageFactory CreateSuffixedStorageFactory(string suffix, IDictionary<string, string> arguments, bool verbose)
        {
            if (string.IsNullOrEmpty(suffix))
            {
                throw new ArgumentNullException("suffix");
            }

            Uri storageBaseAddress = null;
            string storageBaseAddressStr;
            if (arguments.TryGetValue("-storageBaseAddress" + suffix, out storageBaseAddressStr))
            {
                storageBaseAddressStr = storageBaseAddressStr.TrimEnd('/') + "/";

                storageBaseAddress = new Uri(storageBaseAddressStr);
            }

            string storageType;
            if (!arguments.TryGetValue("-storageType" + suffix, out storageType))
            {
                TraceRequiredArgument("-storageType" + suffix);
                return null;
            }

            if (storageType.Equals("File", StringComparison.InvariantCultureIgnoreCase))
            {
                string storagePath;
                if (!arguments.TryGetValue("-storagePath" + suffix, out storagePath))
                {
                    TraceRequiredArgument("-storagePath" + suffix);
                    return null;
                }

                if (storageBaseAddress == null)
                {
                    TraceRequiredArgument("-storageBaseAddress" + suffix);
                    return null;
                }

                return new FileStorageFactory(storageBaseAddress, storagePath) { Verbose = verbose };
            }
            else if (storageType.Equals("Azure", StringComparison.InvariantCultureIgnoreCase))
            {
                string storageAccountName;
                if (!arguments.TryGetValue("-storageAccountName" + suffix, out storageAccountName))
                {
                    TraceRequiredArgument("-storageAccountName" + suffix);
                    return null;
                }

                string storageKeyValue;
                if (!arguments.TryGetValue("-storageKeyValue" + suffix, out storageKeyValue))
                {
                    TraceRequiredArgument("-storageKeyValue" + suffix);
                    return null;
                }

                string storageContainer;
                if (!arguments.TryGetValue("-storageContainer" + suffix, out storageContainer))
                {
                    TraceRequiredArgument("-storageContainer" + suffix);
                    return null;
                }

                string storagePath = null;
                arguments.TryGetValue("-storagePath" + suffix, out storagePath);

                StorageCredentials credentials = new StorageCredentials(storageAccountName, storageKeyValue);
                CloudStorageAccount account = new CloudStorageAccount(credentials, true);
                return new AzureStorageFactory(account, storageContainer, storagePath, storageBaseAddress) { Verbose = verbose };
            }
            else
            {
                Trace.TraceError("Unrecognized storageType \"{0}\"", storageType);
                return null;
            }
        }

        public static bool GetLuceneReset(IDictionary<string, string> arguments)
        {
            string luceneResetStr = "false";
            if (arguments.TryGetValue("-luceneReset", out luceneResetStr))
            {
                return (luceneResetStr.Equals("true", StringComparison.InvariantCultureIgnoreCase));
            }
            else
            {
                return false;
            }
        }

        public static Lucene.Net.Store.Directory GetLuceneDirectory(IDictionary<string, string> arguments)
        {
            IDictionary<string, string> names = new Dictionary<string, string>
            {
                { "directoryType", "-luceneDirectoryType" },
                { "path", "-lucenePath" },
                { "storageAccountName", "-luceneStorageAccountName" },
                { "storageKeyValue", "-luceneStorageKeyValue" },
                { "storageContainer", "-luceneStorageContainer" }
            };

            return GetLuceneDirectoryImpl(arguments, names);
        }

        public static Lucene.Net.Store.Directory GetCopySrcLuceneDirectory(IDictionary<string, string> arguments)
        {
            IDictionary<string, string> names = new Dictionary<string, string>
            {
                { "directoryType", "-srcDirectoryType" },
                { "path", "-srcPath" },
                { "storageAccountName", "-srcStorageAccountName" },
                { "storageKeyValue", "-srcStorageKeyValue" },
                { "storageContainer", "-srcStorageContainer" }
            };

            return GetLuceneDirectoryImpl(arguments, names);
        }

        public static Lucene.Net.Store.Directory GetCopyDestLuceneDirectory(IDictionary<string, string> arguments)
        {
            IDictionary<string, string> names = new Dictionary<string, string>
            {
                { "directoryType", "-destDirectoryType" },
                { "path", "-destPath" },
                { "storageAccountName", "-destStorageAccountName" },
                { "storageKeyValue", "-destStorageKeyValue" },
                { "storageContainer", "-destStorageContainer" }
            };

            return GetLuceneDirectoryImpl(arguments, names);
        }

        public static Lucene.Net.Store.Directory GetLuceneDirectoryImpl(IDictionary<string, string> arguments, IDictionary<string, string> names)
        {
            string luceneDirectoryType;
            if (!arguments.TryGetValue(names["directoryType"], out luceneDirectoryType))
            {
                TraceRequiredArgument(names["directoryType"]);
                return null;
            }

            if (luceneDirectoryType.Equals("File", StringComparison.InvariantCultureIgnoreCase))
            {
                string lucenePath;
                if (!arguments.TryGetValue(names["path"], out lucenePath))
                {
                    TraceRequiredArgument(names["path"]);
                    return null;
                }

                DirectoryInfo directoryInfo = new DirectoryInfo(lucenePath);

                if (!directoryInfo.Exists)
                {
                    directoryInfo.Create();
                    directoryInfo.Refresh();
                }

                return new SimpleFSDirectory(directoryInfo);
            }
            else if (luceneDirectoryType.Equals("Azure", StringComparison.InvariantCultureIgnoreCase))
            {
                string luceneStorageAccountName;
                if (!arguments.TryGetValue(names["storageAccountName"], out luceneStorageAccountName))
                {
                    TraceRequiredArgument(names["storageAccountName"]);
                    return null;
                }

                string luceneStorageKeyValue;
                if (!arguments.TryGetValue(names["storageKeyValue"], out luceneStorageKeyValue))
                {
                    TraceRequiredArgument(names["storageKeyValue"]);
                    return null;
                }

                string luceneStorageContainer;
                if (!arguments.TryGetValue(names["storageContainer"], out luceneStorageContainer))
                {
                    TraceRequiredArgument(names["storageContainer"]);
                    return null;
                }

                StorageCredentials credentials = new StorageCredentials(luceneStorageAccountName, luceneStorageKeyValue);
                CloudStorageAccount account = new CloudStorageAccount(credentials, true);
                return new AzureDirectory(account, luceneStorageContainer);
            }
            else
            {
                Trace.TraceError("Unrecognized Lucene Directory Type \"{0}\"", luceneDirectoryType);
                return null;
            }
        }

        public static Func<HttpMessageHandler> GetHttpMessageHandlerFactory(bool verbose, string catalogBaseAddress = null, string storageBaseAddress = null)
        {
            Func<HttpMessageHandler> handlerFunc = null;
            if (verbose)
            {
                handlerFunc = () =>
                {
                    if (catalogBaseAddress != null)
                    {
                        return new VerboseHandler(new StorageAccessHandler(catalogBaseAddress, storageBaseAddress));
                    }

                    return new VerboseHandler();
                };
            }
            return handlerFunc;
        }

        public static string GetConnectionString(IDictionary<string, string> arguments)
        {
            string connectionString;
            if (!arguments.TryGetValue("-connectionString", out connectionString))
            {
                return null;
            }

            return connectionString;
        }

        public static string GetPath(IDictionary<string, string> arguments)
        {
            string path;
            if (!arguments.TryGetValue("-path", out path))
            {
                return null;
            }

            return path;
        }

        public static string GetElasticsearchEndpoint(IDictionary<string, string> arguments)
        {
            string endpoint;
            if (arguments == null || !arguments.TryGetValue("-elasticsearchendpoint", out endpoint))
            {
                return null;
            }

            return endpoint;
        }

        public static string GetElasticsearchUsername(IDictionary<string, string> arguments)
        {
            string value;
            if (arguments == null || !arguments.TryGetValue("-elasticsearchusername", out value))
            {
                return null;
            }

            return value;
        }

        public static string GetElasticsearchPassword(IDictionary<string, string> arguments)
        {
            string value;
            if (arguments == null || !arguments.TryGetValue("-elasticsearchpassword", out value))
            {
                return null;
            }

            return value;
        }
    }
}
