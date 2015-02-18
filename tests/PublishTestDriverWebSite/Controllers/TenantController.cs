using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json.Linq;
using PublishTestDriverWebSite.Models;
using PublishTestDriverWebSite.Utils;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace PublishTestDriverWebSite.Controllers
{
    [Authorize]
    public class TenantController : Controller
    {
        private string nugetPublishServiceBaseAddress = ConfigurationManager.AppSettings["nuget:PublishServiceBaseAddress"];
        private string nugetPublishServiceResourceId = ConfigurationManager.AppSettings["nuget:PublishServiceResourceId"];
        private const string TenantIdClaimType = "http://schemas.microsoft.com/identity/claims/tenantid";
        private static string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        private static string appKey = ConfigurationManager.AppSettings["ida:AppKey"];

        // GET: Tenant
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public Task<ActionResult> Add()
        {
            return Send("add");
        }

        [HttpPost]
        public Task<ActionResult> Remove()
        {
            return Send("remove");
        }

        async Task<ActionResult> Send(string action)
        {
            string userObjectID = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;
            AuthenticationContext authContext = new AuthenticationContext(Startup.Authority, new NaiveSessionCache(userObjectID));
            ClientCredential credential = new ClientCredential(clientId, appKey);

            AuthenticationResult result = await authContext.AcquireTokenSilentAsync(nugetPublishServiceResourceId, credential, new UserIdentifier(userObjectID, UserIdentifierType.UniqueId));

            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, nugetPublishServiceBaseAddress.TrimEnd('/') + "/tenant/" + action);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

            HttpResponseMessage response = await client.SendAsync(request);

            string message = null;
            if (response.IsSuccessStatusCode)
            {
                message = "OK";
            }
            else
            {
                JObject publishServiceResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
                message = string.Format("ERROR: {0}", publishServiceResponse["error"].ToString());
            }

            return View(new TenantModel { Message = message });
        }
    }
}