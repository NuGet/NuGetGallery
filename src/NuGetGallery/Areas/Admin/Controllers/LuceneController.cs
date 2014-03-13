using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Configuration;

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
            return View("Index", new LuceneInfoModel()
            {
                LastUpdated = await IndexingService.GetLastWriteTime(),
                DocumentCount = await IndexingService.GetDocumentCount(),
                IndexSize = await IndexingService.GetIndexSizeInBytes(),
                Directory = IndexingService.IndexPath,
                IsLocal = IndexingService.IsLocal,
                Location = Config.LuceneIndexLocation
            });
        }

        [HttpPost]
        public virtual Task<ActionResult> Rebuild()
        {
            IndexingService.UpdateIndex(forceRefresh: true);
            return Index();
        }

    }
}
