using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json.Linq;
using PublishTestDriverWebSite.Models;
using PublishTestDriverWebSite.Utils;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace PublishTestDriverWebSite.Controllers
{
    [Authorize]
    public class UploadController : Controller
    {
        private string nugetPublishServiceBaseAddress = ConfigurationManager.AppSettings["nuget:PublishServiceBaseAddress"];
        private string nugetPublishServiceResourceId = ConfigurationManager.AppSettings["nuget:PublishServiceResourceId"];
        private static string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        private static string appKey = ConfigurationManager.AppSettings["ida:AppKey"];

        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> Upload(HttpPostedFileBase uploadFile, string access, string visibility)
        {
            if (uploadFile == null)
            {
                return View("ValidationError", new ValidationErrorModel("please specify a file to upload"));
            }

            string userObjectID = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;
            AuthenticationContext authContext = new AuthenticationContext(Startup.Authority, new NaiveSessionCache(userObjectID));
            ClientCredential credential = new ClientCredential(clientId, appKey);

            AuthenticationResult result = await authContext.AcquireTokenSilentAsync(nugetPublishServiceResourceId, credential, new UserIdentifier(userObjectID, UserIdentifierType.UniqueId));

            string path = GetPath(access == "public", visibility == "hidden");

            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, nugetPublishServiceBaseAddress + path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

            request.Content = new StreamContent(uploadFile.InputStream);

            HttpResponseMessage response;

            try
            {
                response = await client.SendAsync(request);
            }
            catch (Exception e)
            {
                return View("ServiceError", new ServiceErrorModel(e));
            }

            if (response.IsSuccessStatusCode)
            {
                return View(new UploadModel());
            }
            else
            {
                try
                {
                    JObject publishServiceResponse = JObject.Parse(await response.Content.ReadAsStringAsync());

                    string type = publishServiceResponse["type"].ToString();

                    if (type == "ValidationError")
                    {
                        return View(new UploadModel(publishServiceResponse["errors"].Select((t) => t.ToString())));
                    }
                    else
                    {
                        string error = publishServiceResponse["error"].ToString();
                        return View(new UploadModel(string.Format("uploaded file \"{0}\" contains the following errors \"{1}\"", uploadFile.FileName, error)));
                    }
                }
                catch (Exception e)
                {
                    return View("ServiceError", new ServiceErrorModel(e));
                }
            }
        }

        static string GetPath(bool isPublic, bool isHidden)
        {
            StringBuilder path = new StringBuilder("/catalog/apiapp");
            if (isPublic)
            {
                path.Append("/public");
            }
            if (isHidden)
            {
                path.Append("/hidden");
            }
            return path.ToString();
        }
    }
}