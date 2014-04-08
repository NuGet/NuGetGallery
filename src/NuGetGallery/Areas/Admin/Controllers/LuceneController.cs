using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Infrastructure.Lucene;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public partial class LuceneController : AdminControllerBase
    {
        protected IIndexingService IndexingService { get; set; }
        protected IAppConfiguration Config { get; set; }

        protected LuceneController() { }
        public LuceneController(IIndexingService indexingService, IAppConfiguration config)
        {
            IndexingService = indexingService;
            Config = config;
        }

        //
        // GET: /Admin/Lucene/

        public virtual async Task<ActionResult> Index()
        {
            return View("Index",
                await GetLuceneInfo());
        }

        private async Task<LuceneInfoModel> GetLuceneInfo()
        {
            var model = new LuceneInfoModel()
            {
                Directory = IndexingService.IndexPath,
                IsLocal = IndexingService.IsLocal,
                Location = Config.LuceneIndexLocation
            };

            try
            {
                model.LastUpdated = await IndexingService.GetLastWriteTime();
                model.DocumentCount = await IndexingService.GetDocumentCount();
                model.IndexSize = await IndexingService.GetIndexSizeInBytes();
                model.QueryStats = PerfCounters.GetStats(ExternalSearchService.SearchRoundtripTimePerfCounter);
            }
            catch (FileNotFoundException)
            {
                model.LastUpdated = null;
            }
            return model;
        }

        [HttpPost]
        public virtual Task<ActionResult> Rebuild()
        {
            IndexingService.UpdateIndex(forceRefresh: true);
            return Index();
        }

    }
}
