using System.Web.Mvc;

namespace NuGetGallery {
    // TODO: Implement this whole thing!
    public class SearchController : Controller {
        public const string Name = "Search";
        public ActionResult Results(string q) {
            // TODO: Search on the query term (q) and return results.
            return View();
        }
    }
}
