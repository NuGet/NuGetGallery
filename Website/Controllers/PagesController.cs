using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.UI;
using NuGetGallery.ViewModels;

namespace NuGetGallery
{
    public partial class PagesController : Controller
    {
        public IContentService ContentService { get; protected set; }

        protected PagesController() { }
        public PagesController(IContentService contentService)
        {
            ContentService = contentService;
        }

        public virtual ActionResult Contact()
        {
            return View();
        }

        public virtual async Task<ActionResult> Home()
        {
            HtmlString announcement = null;
            HtmlString about = null;
            if (ContentService != null)
            {
                announcement = await ContentService.GetContentItemAsync(
                    Constants.ContentNames.FrontPageAnnouncement,
                    TimeSpan.FromMinutes(1));

                about = await ContentService.GetContentItemAsync(
                    Constants.ContentNames.FrontPageAbout,
                    TimeSpan.FromMinutes(1));
            }

            return View(new HomeViewModel()
            {
                Announcement = announcement,
                About = about
            });
        }

        public virtual ActionResult Terms()
        {
            return View();
        }

        public virtual ActionResult Privacy()
        {
            return View();
        }
    }
}