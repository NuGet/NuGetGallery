using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Owin.Security.OpenIdConnect;
using Newtonsoft.Json.Linq;
using PublishTestDriverWebSite.Models;
using PublishTestDriverWebSite.Utils;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace PublishTestDriverWebSite.Controllers
{
    [Authorize]
    public class ValidateController : Controller
    {
        private string nugetServiceResourceId = ConfigurationManager.AppSettings["nuget:ServiceResourceId"];
        private string nugetPublishServiceBaseAddress = ConfigurationManager.AppSettings["nuget:PublishServiceBaseAddress"];
        private const string TenantIdClaimType = "http://schemas.microsoft.com/identity/claims/tenantid";
        private static string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        private static string appKey = ConfigurationManager.AppSettings["ida:AppKey"];

        public async Task<ActionResult> Index()
        {
            try
            {
                string userObjectID = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;
                AuthenticationContext authContext = new AuthenticationContext(Startup.Authority, new NaiveSessionCache(userObjectID));

                AuthenticationResult authenticationResult;

                if (Startup.Certificate == null)
                {
                    ClientCredential credential = new ClientCredential(clientId, appKey);
                    authenticationResult = await authContext.AcquireTokenSilentAsync(nugetServiceResourceId, credential, new UserIdentifier(userObjectID, UserIdentifierType.UniqueId));
                }
                else
                {
                    ClientAssertionCertificate clientAssertionCertificate = new ClientAssertionCertificate(clientId, Startup.Certificate);
                    authenticationResult = await authContext.AcquireTokenSilentAsync(nugetServiceResourceId, clientAssertionCertificate, new UserIdentifier(userObjectID, UserIdentifierType.UniqueId));
                }

                HttpClient client = new HttpClient();
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, nugetPublishServiceBaseAddress + "/domains");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authenticationResult.AccessToken);
                HttpResponseMessage response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    JArray result = JArray.Parse(json);

                    PublishModel model = new PublishModel();
                    foreach (string s in result.Values().Select(jtoken => jtoken.ToString()))
                    {
                        model.Domains.Add(s);
                    }

                    return View(model);
                }
                else
                {
                    return View(new PublishModel { Message = "Unable to load list of domains" });
                }
            }
            catch (AdalSilentTokenAcquisitionException)
            {
                //TODO: this isn't quite right
                HttpContext.GetOwinContext().Authentication.Challenge(OpenIdConnectAuthenticationDefaults.AuthenticationType);
                return View(new PublishModel { Message = "AuthorizationRequired" });
            }
        }

        [HttpPost]
        public async Task<ActionResult> CheckAccess(string domain, string id)
        {
            try
            {
                string userObjectID = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;
                AuthenticationContext authContext = new AuthenticationContext(Startup.Authority, new NaiveSessionCache(userObjectID));
                ClientCredential credential = new ClientCredential(clientId, appKey);

                AuthenticationResult result = await authContext.AcquireTokenSilentAsync(nugetServiceResourceId, credential, new UserIdentifier(userObjectID, UserIdentifierType.UniqueId));

                string query = string.Format("?domain={0}&id={1}", domain, id);

                HttpClient client = new HttpClient();
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, nugetPublishServiceBaseAddress + "/checkaccess" + query);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

                HttpResponseMessage response = await client.SendAsync(request);

                string message = null;
                if (response.IsSuccessStatusCode)
                {
                    JObject publishServiceResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
                    message = publishServiceResponse["message"].ToString();
                }
                else
                {
                    try
                    {
                        JObject publishServiceResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
                        string error = publishServiceResponse["error"].ToString();
                        message = string.Format("checkaccess error \"{0}\"", error);
                    }
                    catch { }
                }

                return View(new CheckAccessModel { Id = id, Message = message ?? string.Format("{0} with no further details available", response.StatusCode) });
            }
            catch (AdalSilentTokenAcquisitionException)
            {
                //TODO: this isn't quite right
                HttpContext.GetOwinContext().Authentication.Challenge(OpenIdConnectAuthenticationDefaults.AuthenticationType);
                return View(new CheckAccessModel { Message = "AuthorizationRequired" });
            }
        }
    }
}
