using System;
using System.Collections.Generic;
using System.Linq;
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

        public virtual ActionResult Index()
        {
            return View("Index", new LuceneInfoModel()
            {
                LastUpdated = IndexingService.GetLastWriteTime(),
                DocumentCount = IndexingService.GetDocumentCount(),
                IndexSize = IndexingService.GetIndexSizeInBytes(),
                Directory = IndexingService.IndexPath,
                Location = Config.LuceneIndexLocation
            });
        }

        [HttpPost]
        public virtual ActionResult Rebuild()
        {
            IndexingService.UpdateIndex(forceRefresh: true);
            return Index();
        }

    }
}
