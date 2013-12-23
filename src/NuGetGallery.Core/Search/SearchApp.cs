using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Microsoft.Owin;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGetGallery;
using Owin;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Web.Configuration;

namespace NuGetGallery
{
    public class SearchApp
    {
        static PackageSearcherManager _searcherManager;

        public static Action<IAppBuilder> BuildSearch()
        {
            return app =>
            {
                app.Run(context =>
                {
                    Trace.TraceInformation("Search: {0}", context.Request.QueryString);

                    try
                    {
                        InitializeSearcherManager();

                        string q = context.Request.Query["q"];

                        string projectType = context.Request.Query["projectType"];

                        bool luceneQuery;
                        if (!bool.TryParse(context.Request.Query["luceneQuery"], out luceneQuery))
                        {
                            luceneQuery = true;
                        }

                        bool includePrerelease;
                        if (!bool.TryParse(context.Request.Query["prerelease"], out includePrerelease))
                        {
                            includePrerelease = false;
                        }

                        bool countOnly;
                        if (!bool.TryParse(context.Request.Query["countOnly"], out countOnly))
                        {
                            countOnly = false;
                        }

                        string sortBy = context.Request.Query["sortBy"] ?? "relevance";

                        string feed = context.Request.Query["feed"] ?? "none";

                        int skip;
                        if (!int.TryParse(context.Request.Query["skip"], out skip))
                        {
                            skip = 0;
                        }

                        int take;
                        if (!int.TryParse(context.Request.Query["take"], out take))
                        {
                            take = 20;
                        }

                        bool includeExplanation;
                        if (!bool.TryParse(context.Request.Query["explanation"], out includeExplanation))
                        {
                            includeExplanation = false;
                        }

                        bool ignoreFilter;
                        if (!bool.TryParse(context.Request.Query["ignoreFilter"], out ignoreFilter))
                        {
                            ignoreFilter = false;
                        }

                        string args = string.Format("Searcher.Search(..., {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10})", q, countOnly, projectType, includePrerelease, feed, sortBy, skip, take, includeExplanation, ignoreFilter, luceneQuery);
                        Trace.TraceInformation(args);

                        if (!luceneQuery)
                        {
                            q = LuceneQueryCreator.Parse(q);
                        }

                        string content = Searcher.Search(_searcherManager, q, countOnly, projectType, includePrerelease, feed, sortBy, skip, take, includeExplanation, ignoreFilter);

                        return MakeResponse(context, content);
                    }
                    catch (CorruptIndexException e)
                    {
                        _searcherManager = null;
                        TraceException(e);
                        throw;
                    }
                    catch (Exception e)
                    {
                        TraceException(e);
                        throw;
                    }
                });
            };
        }

        public static Action<IAppBuilder> BuildRange()
        {
            return app =>
            {
                app.Run(context =>
                {
                    Trace.TraceInformation("Range: {0}", context.Request.QueryString);

                    try
                    {
                        InitializeSearcherManager();

                        string min = context.Request.Query["min"];
                        string max = context.Request.Query["max"];

                        string content = "[]";

                        int minKey;
                        int maxKey;
                        if (min != null && max != null && int.TryParse(min, out minKey) && int.TryParse(max, out maxKey))
                        {
                            Trace.TraceInformation("Searcher.KeyRangeQuery(..., {0}, {1})", minKey, maxKey);

                            content = Searcher.KeyRangeQuery(_searcherManager, minKey, maxKey);
                        }

                        return MakeResponse(context, content);
                    }
                    catch (CorruptIndexException e)
                    {
                        _searcherManager = null;
                        TraceException(e);
                        throw;
                    }
                    catch (Exception e)
                    {
                        TraceException(e);
                        throw;
                    }
                });
            };
        }

        public static Action<IAppBuilder> BuildDiag()
        {
            return app =>
            {
                app.Run(context =>
                {
                    Trace.TraceInformation("Diag");

                    try
                    {
                        InitializeSearcherManager();

                        return MakeResponse(context, IndexAnalyzer.Analyze(_searcherManager));
                    }
                    catch (CorruptIndexException e)
                    {
                        _searcherManager = null;
                        TraceException(e);
                        throw;
                    }
                    catch (Exception e)
                    {
                        TraceException(e);
                        throw;
                    }
                });
            };
        }

        public static Action<IAppBuilder> BuildFields()
        {
            return app =>
            {
                app.Run(context =>
                {
                    Trace.TraceInformation("Fields");

                    try
                    {
                        InitializeSearcherManager();

                        return MakeResponse(context, IndexAnalyzer.GetDistinctStoredFieldNames(_searcherManager));
                    }
                    catch (CorruptIndexException e)
                    {
                        _searcherManager = null;
                        TraceException(e);
                        throw;
                    }
                    catch (Exception e)
                    {
                        TraceException(e);
                        throw;
                    }
                });
            };
        }

        public static Action<IAppBuilder> BuildWhere()
        {
            return app =>
            {
                app.Run(context =>
                {
                    Trace.TraceInformation("Where");

                    JObject response = new JObject();

                    if (GetUseStorageConfiguration())
                    {
                        string storageConnectionString = WebConfigurationManager.AppSettings["StorageConnectionString"];
                        CloudStorageAccount cloudStorageAccount;
                        if (storageConnectionString != null && CloudStorageAccount.TryParse(storageConnectionString, out cloudStorageAccount))
                        {
                            string accountName = cloudStorageAccount.Credentials.AccountName;
                            response.Add("AccountName", accountName);

                            string storageContainer = WebConfigurationManager.AppSettings["StorageContainer"];
                            response.Add("StorageContainer", storageContainer);

                            CloudBlobClient client = cloudStorageAccount.CreateCloudBlobClient();
                            CloudBlobContainer container = client.GetContainerReference(storageContainer);
                            CloudBlockBlob blob = container.GetBlockBlobReference("segments.gen");

                            response.Add("IndexExists", blob.Exists());
                        }
                        else
                        {
                            response.Add("AccountName", null);
                        }
                    }
                    else
                    {
                        response.Add("FileSystemPath", WebConfigurationManager.AppSettings["FileSystemPath"]);
                    }

                    return MakeResponse(context, response.ToString());
                });
            };
        }

        private static Task MakeResponse(IOwinContext context, string content)
        {
            context.Response.Headers.Add("Pragma", new string[] { "no-cache" });
            context.Response.Headers.Add("Cache-Control", new string[] { "no-cache" });
            context.Response.Headers.Add("Expires", new string[] { "0" });
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync(content);
        }

        private static void InitializeSearcherManager()
        {
            if (_searcherManager == null)
            {
                Trace.TraceInformation("InitializeSearcherManager: new PackageSearcherManager");

                Lucene.Net.Store.Directory directory = GetDirectory();
                Rankings rankings = GetRankings();
                _searcherManager = new PackageSearcherManager(directory, rankings);
            }
        }

        private static Lucene.Net.Store.Directory GetDirectory()
        {
            if (GetUseStorageConfiguration())
            {
                string storageContainer = WebConfigurationManager.AppSettings["StorageContainer"];
                string storageConnectionString = WebConfigurationManager.AppSettings["StorageConnectionString"];

                Trace.TraceInformation("GetDirectory using storage. Container: {0}", storageContainer);

                return new AzureDirectory(CloudStorageAccount.Parse(storageConnectionString), storageContainer, new RAMDirectory());
            }
            else
            {
                string fileSystemPath = WebConfigurationManager.AppSettings["FileSystemPath"];

                Trace.TraceInformation("GetDirectory using filesystem. Folder: {0}", fileSystemPath);

                return new SimpleFSDirectory(new DirectoryInfo(fileSystemPath));
            }
        }

        private static Rankings GetRankings()
        {
            if (GetUseStorageConfiguration())
            {
                string storageConnectionString = WebConfigurationManager.AppSettings["StorageConnectionString"];

                Trace.TraceInformation("Rankings from storage.");

                return new StorageRankings(storageConnectionString);
            }
            else
            {
                string folder = WebConfigurationManager.AppSettings["FileSystemPathRankings"];

                Trace.TraceInformation("Rankings from folder.");

                return new FolderRankings(folder);
            }
        }

        private static bool GetUseStorageConfiguration()
        {
            string useStorageStr = WebConfigurationManager.AppSettings["UseStorage"];
            bool useStorage = false;
            bool.TryParse(useStorageStr ?? "false", out useStorage);
            return useStorage;
        }

        private static void TraceException(Exception e)
        {
            Trace.TraceError(e.GetType().Name);
            Trace.TraceError(e.Message);
            Trace.TraceError(e.StackTrace);

            if (e.InnerException != null)
            {
                TraceException(e.InnerException);
            }
        }
    }
}