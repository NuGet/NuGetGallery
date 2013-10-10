using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery
{
    public partial class ContentPagesController : AppController
    {
        public IContentService ContentService { get; set; }

        protected ContentPagesController() { }

        public ContentPagesController(IContentService contentService)
        {
            ContentService = contentService;
        }

        public virtual Task<ActionResult> Home()
        {
            return Page("Home");
        }

        public virtual async Task<ActionResult> Page(string name)
        {
            string title = name.Replace('-', ' ');
            ViewBag.Title = title;
            ViewBag.Tab = title; // may or may not actually match a tab name :)
            ViewBag.Content = await ContentService.GetContentItemAsync(
                name,
                TimeSpan.FromDays(1));

            return View("Page");
        }
    }
}