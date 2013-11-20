using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGetGallery;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;

namespace SearchService.Controllers
{
    public class ApiController : Controller
    {
        static PackageSearcherManager _searcherManager;

        public ApiController()
        {
            Trace.TraceInformation("ApiController constructor");
        }

        //
        // GET: /search

        [ActionName("Search")]
        [HttpGet]
        public virtual ActionResult Search()
        {
            Trace.TraceInformation("Search: {0}", Request.QueryString.ToString());

            try
            {
                InitializeSearcherManager();

                string q = Request.QueryString["q"];

                string projectType = Request.QueryString["projectType"];

                bool includePrerelease;
                if (!bool.TryParse(Request.QueryString["prerelease"], out includePrerelease))
                {
                    includePrerelease = false;
                }

                bool countOnly;
                if (!bool.TryParse(Request.QueryString["countOnly"], out countOnly))
                {
                    countOnly = false;
                }

                string sortBy = Request.QueryString["sortBy"] ?? "relevance";

                string feed = Request.QueryString["feed"] ?? "none";

                int page;
                if (!int.TryParse(Request.QueryString["page"], out page))
                {
                    page = 1;
                }

                bool includeExplanation;
                if (!bool.TryParse(Request.QueryString["explanation"], out includeExplanation))
                {
                    includeExplanation = false;
                }

                bool ignoreFilter;
                if (!bool.TryParse(Request.QueryString["ignoreFilter"], out ignoreFilter))
                {
                    ignoreFilter = false;
                }

                Trace.TraceInformation("Searcher.Search(..., {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8})", q, countOnly, projectType, includePrerelease, feed, sortBy, page, includeExplanation, ignoreFilter);

                string content = Searcher.Search(_searcherManager, q, countOnly, projectType, includePrerelease, feed, sortBy, page, includeExplanation, ignoreFilter);

                return MakeResponse(content);
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
        }

        //
        // GET: /range

        [ActionName("Range")]
        [HttpGet]
        public virtual ActionResult Range()
        {
            Trace.TraceInformation("Range: {0}", Request.QueryString.ToString());

            try
            {
                InitializeSearcherManager();

                string min = Request.QueryString["min"];
                string max = Request.QueryString["max"];

                string content = "[]";

                int minKey;
                int maxKey;
                if (min != null && max != null && int.TryParse(min, out minKey) && int.TryParse(max, out maxKey))
                {
                    Trace.TraceInformation("Searcher.KeyRangeQuery(..., {0}, {1})", minKey, maxKey);

                    content = Searcher.KeyRangeQuery(_searcherManager, minKey, maxKey);
                }

                return MakeResponse(content);
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
        }

        //
        // GET: /diag

        [ActionName("Diag")]
        [HttpGet]
        public virtual ActionResult Diag()
        {
            Trace.TraceInformation("Diag");

            try
            {
                InitializeSearcherManager();

                return MakeResponse(IndexAnalyzer.Analyze(_searcherManager));
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
        }

        //
        // GET: /fields

        [ActionName("Fields")]
        [HttpGet]
        public virtual ActionResult Fields()
        {
            Trace.TraceInformation("Fields");

            try
            {
                InitializeSearcherManager();

                return MakeResponse(IndexAnalyzer.GetDistinctStoredFieldNames(_searcherManager));
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
        }

        //
        // GET: /where

        [ActionName("Where")]
        [HttpGet]
        public virtual ActionResult Where()
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

            return MakeResponse(response.ToString());
        }

        //  private helpers

        private ActionResult MakeResponse(string content)
        {
            HttpContext.Response.AddHeader("Pragma", "no-cache");
            HttpContext.Response.AddHeader("Cache-Control", "no-cache");
            HttpContext.Response.AddHeader("Expires", "0");

            return new ContentResult
            {
                Content = content,
                ContentType = "application/json"
            };
        }

        private static void InitializeSearcherManager()
        {
            if (_searcherManager == null)
            {
                Trace.TraceInformation("InitializeSearcherManager: new PackageSearcherManager");

                Lucene.Net.Store.Directory directory = GetDirectory();
                _searcherManager = new PackageSearcherManager(directory);
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

        private static bool GetUseStorageConfiguration()
        {
            string useStorageStr = WebConfigurationManager.AppSettings["UseStorage"];
            bool useStorage = false;
            bool.TryParse(useStorageStr ?? "false", out useStorage);
            return useStorage;
        }

        private void TraceException(Exception e)
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
