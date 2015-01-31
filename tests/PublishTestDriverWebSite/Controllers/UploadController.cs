using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json.Linq;
using PublishTestDriverWebSite.Models;
using PublishTestDriverWebSite.Utils;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
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
        private const string TenantIdClaimType = "http://schemas.microsoft.com/identity/claims/tenantid";
        private static string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        private static string appKey = ConfigurationManager.AppSettings["ida:AppKey"];

        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> Upload(HttpPostedFileBase uploadFile)
        {
            string userObjectID = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;
            AuthenticationContext authContext = new AuthenticationContext(Startup.Authority, new NaiveSessionCache(userObjectID));
            ClientCredential credential = new ClientCredential(clientId, appKey);

            AuthenticationResult result = await authContext.AcquireTokenSilentAsync(nugetPublishServiceResourceId, credential, new UserIdentifier(userObjectID, UserIdentifierType.UniqueId));

            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, nugetPublishServiceBaseAddress + "/catalog/microservices");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

            request.Content = new StreamContent(uploadFile.InputStream);
            HttpResponseMessage response = await client.SendAsync(request);

            string message = null;
            if (response.IsSuccessStatusCode)
            {
                message = "success";
            }
            else
            {
                try
                {
                    JObject publishServiceResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
                    string error = publishServiceResponse["error"].ToString();
                    message = string.Format("uploaded file \"{0}\" contains the following errors \"{1}\"", uploadFile.FileName, error);
                }
                catch { }
            }

            return View(new UploadModel { Message = message ?? string.Format("{0} with no further details available", response.StatusCode)});
        }
    }
}