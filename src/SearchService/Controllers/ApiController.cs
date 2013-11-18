﻿using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json.Linq;
using NuGetGallery;
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

            string content = Searcher.Search(_searcherManager, q, countOnly, projectType, includePrerelease, feed, sortBy, page, includeExplanation, ignoreFilter);

            return MakeResponse(content);
        }

        //
        // GET: /range

        [ActionName("Range")]
        [HttpGet]
        public virtual ActionResult Range()
        {
            Trace.TraceInformation("Range: {0}", Request.QueryString.ToString());

            InitializeSearcherManager();

            string min = Request.QueryString["min"];
            string max = Request.QueryString["max"];

            string content = "[]";

            int minKey;
            int maxKey;
            if (min != null && max != null && int.TryParse(min, out minKey) && int.TryParse(max, out maxKey))
            {
                content = Searcher.KeyRangeQuery(_searcherManager, minKey, maxKey);
            }

            return MakeResponse(content);
        }

        //
        // GET: /diag

        [ActionName("Diag")]
        [HttpGet]
        public virtual ActionResult Diag()
        {
            Trace.TraceInformation("Diag");

            InitializeSearcherManager();

            return MakeResponse(IndexAnalyzer.Analyze(_searcherManager));
        }

        //
        // GET: /fields

        [ActionName("Fields")]
        [HttpGet]
        public virtual ActionResult Fields()
        {
            Trace.TraceInformation("Fields");

            InitializeSearcherManager();

            return MakeResponse(IndexAnalyzer.GetDistinctStoredFieldNames(_searcherManager));
        }

        //
        // GET: /where

        [ActionName("Where")]
        [HttpGet]
        public virtual ActionResult Where()
        {
            Trace.TraceInformation("Where");

            JObject response = new JObject();
            
            bool useStorage = bool.Parse(WebConfigurationManager.AppSettings["UseStorage"]);

            response.Add("UseStorage", useStorage);
            
            if (useStorage)
            {
                response.Add("StorageContainer", WebConfigurationManager.AppSettings["StorageContainer"]);
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
            bool useStorage = bool.Parse(WebConfigurationManager.AppSettings["UseStorage"]);
            if (useStorage)
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
    }
}
