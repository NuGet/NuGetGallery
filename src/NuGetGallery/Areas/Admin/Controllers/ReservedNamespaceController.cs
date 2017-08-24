using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        public IReservedNamespaceService ReservedNamespaceService { get; private set; }

        protected ReservedNamespaceController()
        {
        }

        public ReservedNamespaceController(IEntitiesContext entitiesContext, IReservedNamespaceService reservedNamespaceService)
        {
            EntitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            ReservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
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
            var foundPrefixes = ReservedNamespaceService.FindReservedNamespacesForPrefixList(prefixQueries.ToList());
            var notFoundPrefixes = prefixQueries.Except(foundPrefixes.Select(p => p.Value));
            var results = new
            {
                FoundPrefixes = foundPrefixes.Select(
                    p => new ReservedNamespace(p.Value, p.IsSharedNamespace, p.IsPrefix)),
                NotFoundPrefixes = notFoundPrefixes.Select(
                    p => new ReservedNamespace(p.ToString(), isSharedNamespace: false, isPrefix: true))
            };

            return Json(results, JsonRequestBehavior.AllowGet);
        }

        public async Task<JsonResult> Update(List<string> namespaceJson)
        {
            await Task.Yield();
            return null;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> AddPrefix(ReservedNamespace prefix)
        {
            try
            {
                await ReservedNamespaceService.AddReservedNamespaceAsync(prefix);
                await ReservedNamespaceService.AddOwnerToReservedNamespaceAsync(prefix.Value, "shishirx34");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Issue in Add Prefix: " + ex.Message);
                return Json(new { success = false });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> RemovePrefix(ReservedNamespace prefix)
        {
            try
            {
                // Only need to delete the prefix, the cascade delete will take care of removing owners and package registration mappings
                await ReservedNamespaceService.DeleteReservedNamespaceAsync(prefix.Value);
                //await ReservedNamespaceService.DeleteUserFromReservedNamespaceAsync(prefix, new User("shishirx34"));
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return Json(new { success = false });
            }

        }

        private static string[] GetPrefixesFromQuery(string query)
        {
            return query.Split(',', '\r', '\n',';')
                .Select(prefix => prefix.Trim())
                .Where(prefix => !string.IsNullOrEmpty(prefix)).ToArray();
        }
    }
}