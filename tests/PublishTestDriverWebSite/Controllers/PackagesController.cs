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
        private string nugetServiceResourceId = ConfigurationManager.AppSettings["nuget:ServiceResourceId"];

        private string nugetPublishServiceBaseAddress = ConfigurationManager.AppSettings["nuget:PublishServiceBaseAddress"];
        private string nugetSearchServiceBaseAddress = ConfigurationManager.AppSettings["nuget:SearchServiceBaseAddress"];
        
        private static string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        private static string aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
        private static string appKey = ConfigurationManager.AppSettings["ida:AppKey"];

        //  The idea of the packages page is that it gets the list of packages from Lucene (from the Search Service)
        //  but when the owner is the current user (if there is a current user), packages owned by him are highlighted.
        //  So far this packages page just gets the list of registrations for which the current user is an owner.

        // GET: Packages
        public async Task<ActionResult> Index()
        {
            try
            {
                string signedInUserID = ClaimsPrincipal.Current.FindFirst(ClaimTypes.NameIdentifier).Value;
                string tenantId = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid").Value;

                string authority = string.Format(aadInstance, tenantId);
                AuthenticationContext authContext = new AuthenticationContext(authority, new NaiveSessionCache(signedInUserID));

                ClientAssertionCertificate clientAssertionCertificate = new ClientAssertionCertificate(clientId, Startup.Certificate);
                AuthenticationResult authenticationResult = await authContext.AcquireTokenSilentAsync(nugetServiceResourceId, clientAssertionCertificate, new UserIdentifier(signedInUserID, UserIdentifierType.UniqueId));

                HttpClient client = new HttpClient();
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, nugetSearchServiceBaseAddress + "/secure/query");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authenticationResult.AccessToken);

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
                    string json = await response.Content.ReadAsStringAsync();
                    return View(new PackagesModel(JObject.Parse(json)));
                }
                else
                {
                    string err = await response.Content.ReadAsStringAsync();
                    return View("ServiceError", new ServiceErrorModel(response.StatusCode, err));
                }
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