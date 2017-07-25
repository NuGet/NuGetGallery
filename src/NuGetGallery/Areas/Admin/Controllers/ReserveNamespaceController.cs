using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class ReserveNamespaceController : AdminControllerBase
    {
        protected IEntitiesContext EntitiesContext { get; set; }

        protected ReserveNamespaceController()
        {
        }

        public ReserveNamespaceController(IEntitiesContext entitiesContext)
        {
            EntitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
        }

        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public virtual JsonResult SearchPrefix(string query)
        {
            // TODO: validate query
            var matchingPrefixes = EntitiesContext.ReservedNamespaces
                .Where(prefix => prefix.Value.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var nonMatchingPrefixes = matchingPrefixes.Count == 0 ? query : null;
            var results = new
            {
                MatchingPrefixes = matchingPrefixes
                    .Select(p => new {
                        Prefix = p
                    }),
                NonMatchingPrefix = nonMatchingPrefixes
                    .Select(p => new {
                        Prefix = new ReservedNamespace(p.ToString(), isSharedNamespace: false, isExactMatch: true)
                    })
            };

            return Json(results, JsonRequestBehavior.AllowGet);
        }

        public async Task<JsonResult> Update(List<string> namespaceJson)
        {
            await Task.Yield();
            return null;
        }
    }
}