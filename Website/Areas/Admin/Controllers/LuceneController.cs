using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public partial class LuceneController : AdminControllerBase
    {
        protected IIndexingService IndexingService { get; set; }

        protected LuceneController() { }
        public LuceneController(IIndexingService indexingService)
        {
            IndexingService = indexingService;
        }

        //
        // GET: /Admin/Lucene/

        public virtual ActionResult Index()
        {
            return View("Index", new LuceneInfoModel()
            {
                LastUpdated = IndexingService.GetLastWriteTime()
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
