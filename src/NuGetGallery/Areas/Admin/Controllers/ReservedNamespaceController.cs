using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class ReservedNamespaceController : AdminControllerBase
    {
        protected IEntitiesContext EntitiesContext { get; set; }

        protected ReservedNamespaceController()
        {
        }

        public ReservedNamespaceController(IEntitiesContext entitiesContext)
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
            var prefixQueries = GetPrefixesFromQuery(query);
            var foundPrefixes = EntitiesContext.ReservedNamespaces.Where(p => 
                    prefixQueries.Any(qPrefix => p.Value == qPrefix))
                .ToList();

            var notFoundPrefixes = prefixQueries.Except(foundPrefixes.Select(p => p.Value));
            var results = new
            {
                FoundPrefixes = foundPrefixes?.Select(
                    p => new ReservedNamespace(p.Value, p.IsSharedNamespace, p.IsPrefix)),
                NotFoundPrefixes = notFoundPrefixes?.Select(
                    p => new ReservedNamespace(p.ToString(), isSharedNamespace: false, isExactMatch: true))
            };

            return Json(results, JsonRequestBehavior.AllowGet);
        }

        public async Task<JsonResult> Update(List<string> namespaceJson)
        {
            await Task.Yield();
            return null;
        }

        private static string[] GetPrefixesFromQuery(string query)
        {
            return query.Split(',', '\r', '\n',';')
                .Select(prefix => prefix.Trim())
                .Where(prefix => !string.IsNullOrEmpty(prefix)).ToArray();
        }
    }
}