using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Owin.Security.OpenIdConnect;
using Newtonsoft.Json.Linq;
using PublishTestDriverWebSite.Models;
using PublishTestDriverWebSite.Utils;
using System;
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
    public class PackagesController : Controller
    {
        private string nugetPublishServiceResourceId = ConfigurationManager.AppSettings["nuget:PublishServiceResourceId"];
        private string nugetPublishServiceBaseAddress = ConfigurationManager.AppSettings["nuget:PublishServiceBaseAddress"];

        private string nugetSearchServiceResourceId = ConfigurationManager.AppSettings["nuget:SearchServiceResourceId"];
        private string nugetSearchServiceBaseAddress = ConfigurationManager.AppSettings["nuget:SearchServiceBaseAddress"];
        
        private const string TenantIdClaimType = "http://schemas.microsoft.com/identity/claims/tenantid";
        private static string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        private static string appKey = ConfigurationManager.AppSettings["ida:AppKey"];

        //  The idea of the packages page is that it gets the list of packages from Lucene (from the Search Service)
        //  but when the owner is the current user (if there is a current user), packages owned by him are highlighted.
        //  So far this packages page just gets the list of registrations for which the current user is an owner.

        // GET: Packages
        public async Task<ActionResult> Index()
        {
            AuthenticationResult result = null;

            try
            {
                string userObjectID = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;
                AuthenticationContext authContext = new AuthenticationContext(Startup.Authority, new NaiveSessionCache(userObjectID));
                ClientCredential credential = new ClientCredential(clientId, appKey);

                result = await authContext.AcquireTokenSilentAsync(nugetPublishServiceResourceId, credential, new UserIdentifier(userObjectID, UserIdentifierType.UniqueId));

                HttpClient client = new HttpClient();
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, nugetSearchServiceBaseAddress + "/query");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
                HttpResponseMessage response = await client.SendAsync(request);

                string json = await response.Content.ReadAsStringAsync();

                PackagesModel model = new PackagesModel { Raw = json };

                return View(model);
            }
            catch (Exception e)
            {
                if (Request.QueryString["reauth"] == "True")
                {
                    //
                    // Send an OpenID Connect sign-in request to get a new set of tokens.
                    // If the user still has a valid session with Azure AD, they will not be prompted for their credentials.
                    // The OpenID Connect middleware will return to this controller after the sign-in response has been handled.
                    //
                    HttpContext.GetOwinContext().Authentication.Challenge(OpenIdConnectAuthenticationDefaults.AuthenticationType);
                }

                //
                // The user needs to re-authorize.  Show them a message to that effect.
                //
                ViewBag.ErrorMessage = "AuthorizationRequired";

                return View();
            }


            //
            // If the call failed for any other reason, show the user an error.
            //
            return View("Error");
        }
    }
}