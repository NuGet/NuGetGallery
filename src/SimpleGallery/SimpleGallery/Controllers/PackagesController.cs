using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json.Linq;
using SimpleGalleryLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.Http;

namespace SimpleGallery.Controllers
{
    public class PackagesController : ApiController
    {
        // process the nupkg upload
        public async Task<HttpResponseMessage> PostFormData()
        {
            if (Request.Content.IsMimeMultipartContent())
            {
                string root = HttpContext.Current.Server.MapPath("~/App_Data");
                var provider = new MultipartFormDataStreamProvider(root);

                try
                {
                    await Request.Content.ReadAsMultipartAsync(provider);

                    foreach (MultipartFileData data in provider.FileData)
                    {
                        FileInfo file = new FileInfo(data.LocalFileName);

                        string connectionString = WebConfigurationManager.AppSettings["StorageConnectionString"];

                        CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);
                        SimpleGalleryAPI.UploadPackageToTemp(account, file);
                    }

                    var response = Request.CreateResponse(HttpStatusCode.Moved);
                    response.Headers.Location = new Uri("http://simplegallery.azurewebsites.net/");
                    return response;
                }
                catch (Exception e)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, e);
                }
            }
            else
            {
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
            }
        }
    }
}
