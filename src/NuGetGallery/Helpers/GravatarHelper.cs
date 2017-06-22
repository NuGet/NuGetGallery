using System.Web;
using Microsoft.Web.Helpers;

namespace NuGetGallery.Helpers
{
    public static class GravatarHelper
    {
        public static string Url(string email, int size)
        {
            var url = Gravatar.GetUrl(email, size, "retro", GravatarRating.G);

            if (url != null && HttpContext.Current.Request.IsSecureConnection)
            {
                url = url.Replace("http://www.gravatar.com/", "https://secure.gravatar.com/");
            }

            return HttpUtility.HtmlDecode(url);
        }

        public static HtmlString Image(string email, int size, object attributes = null)
        {
            var gravatarHtml = Gravatar.GetHtml(email, size, "retro", GravatarRating.G, attributes: attributes);

            if (gravatarHtml != null && HttpContext.Current.Request.IsSecureConnection)
            {
                gravatarHtml = new HtmlString(gravatarHtml.ToHtmlString().Replace("http://www.gravatar.com/", "https://secure.gravatar.com/"));
            }

            return gravatarHtml;
        }
    }
}