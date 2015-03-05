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
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace PublishTestDriverWebSite.Controllers
{
    [Authorize]
    public class UploadController : Controller
    {
        private static string nugetServiceResourceId = ConfigurationManager.AppSettings["nuget:ServiceResourceId"];
        private static string nugetPublishServiceBaseAddress = ConfigurationManager.AppSettings["nuget:PublishServiceBaseAddress"];
        private static string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        private static string appKey = ConfigurationManager.AppSettings["ida:AppKey"];
        private static string aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];

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

            string signedInUserID = ClaimsPrincipal.Current.FindFirst(ClaimTypes.NameIdentifier).Value;
            string tenantId = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid").Value;

            string authority = string.Format(aadInstance, tenantId);

            AuthenticationContext authContext = new AuthenticationContext(authority, new NaiveSessionCache(signedInUserID));

            ClientAssertionCertificate clientAssertionCertificate = new ClientAssertionCertificate(clientId, Startup.Certificate);
            AuthenticationResult authenticationResult = await authContext.AcquireTokenSilentAsync(nugetServiceResourceId, clientAssertionCertificate, new UserIdentifier(signedInUserID, UserIdentifierType.UniqueId));

            string requestUri = nugetPublishServiceBaseAddress.TrimEnd('/') + "/catalog/apiapp";

            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authenticationResult.AccessToken);

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

        static string MakeQuery(string organization, string subscription)
        {
            if (organization != null)
            {
                return "?organization=" + organization;
            }
            if (subscription != null)
            {
                return "?subscription=" + subscription;
            }
            return string.Empty;
        }
    }
}