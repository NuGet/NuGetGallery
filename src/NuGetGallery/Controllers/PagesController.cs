using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace NuGetGallery
{
    public partial class PagesController : AppController
    {
        public IContentService ContentService { get; protected set; }

        protected PagesController() { }
        public PagesController(IContentService contentService)
        {
            ContentService = contentService;
        }

        // This will let you add 'static' cshtml pages to the site under View/Pages or Branding/Views/Pages
        public virtual ActionResult Page(string pageName)
        {
            // Prevent traversal attacks and serving non-pages by disallowing ., /, %, and more!
            if (pageName == null || pageName.Any(c => !Char.IsLetterOrDigit(c)))
            {
                return HttpNotFound();
            }

            return View(pageName);
        }

        public virtual ActionResult About()
        {
            return View();
        }

        public virtual ActionResult Contact()
        {
            return View();
        }

        public virtual async Task<ActionResult> Home()
        {
            if (ContentService != null)
            {
                ViewBag.Content = await ContentService.GetContentItemAsync(
                    Constants.ContentNames.Home,
                    TimeSpan.FromMinutes(1));
            }
            return View();
        }

        public virtual ActionResult EmptyHome()
        {
            return new HttpStatusCodeResult(HttpStatusCode.OK, "Empty Home");
        }

        public virtual async Task<ActionResult> Terms()
        {
            if (ContentService != null)
            {
                ViewBag.Content = await ContentService.GetContentItemAsync(
                    Constants.ContentNames.TermsOfUse,
                    TimeSpan.FromDays(1));
            }
            return View();
        }

        public virtual async Task<ActionResult> Privacy()
        {
            if (ContentService != null)
            {
                ViewBag.Content = await ContentService.GetContentItemAsync(
                    Constants.ContentNames.PrivacyPolicy,
                    TimeSpan.FromDays(1));
            }
            return View();
        }
    }
}