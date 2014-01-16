using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.IO;

namespace NuGetGallery.FunctionTests.Helpers
{
    /// <summary>
    /// This class has the helper methods to do gallery operations via OData.
    /// </summary>
    public class ODataHelper
    {
        public static Task<string> DownloadPackageFromFeed(string packageId, string version, string operation = "Install")
        {           
            HttpClient client = new HttpClient();
            string requestUri = UrlHelper.V2FeedRootUrl + @"Package/" + packageId + @"/" + version;

            CancellationTokenSource cts = new CancellationTokenSource();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("user-agent", "TestAgent");
            request.Headers.Add("NuGet-Operation", operation);
            Task<HttpResponseMessage> responseTask = client.SendAsync(request);

            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();
            responseTask.ContinueWith((rt) =>
            {
                HttpResponseMessage responseMessage = rt.Result;
                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    try
                    {
                        string filename;
                        ContentDispositionHeaderValue contentDisposition = responseMessage.Content.Headers.ContentDisposition;
                        if (contentDisposition != null)
                        {
                            filename = contentDisposition.FileName;
                        }
                        else
                        {
                            filename = packageId; // if file name not present set the package Id for the file name.
                        }
                        FileStream fileStream = File.Create(filename);
                        Task contentTask = responseMessage.Content.CopyToAsync(fileStream);
                        contentTask.ContinueWith((ct) =>
                        {
                            try
                            {
                                fileStream.Close();
                                tcs.SetResult(filename);
                            }
                            catch (Exception e)
                            {
                                tcs.SetException(e);
                            }
                        });
                    }
                    catch (Exception e)
                    {
                        tcs.SetException(e);
                    }
                }
                else
                {
                    string msg = string.Format("Http StatusCode: {0}", responseMessage.StatusCode);
                    tcs.SetException(new ApplicationException(msg));
                }
            });

            return tcs.Task;
        }
                
        public static async Task<string> TryDownloadPackageFromFeed(string packageId, string version)
        {
            try
            {
                HttpClientHandler handler = new HttpClientHandler();
                handler.AllowAutoRedirect = false;
                using (HttpClient client = new HttpClient(handler))
                {
                    string requestUri = UrlHelper.V2FeedRootUrl + @"Package/" + packageId + @"/" + version;
                    var response = await client.GetAsync(requestUri);
                    //print the header 
                    Console.WriteLine("HTTP status code : {0}", response.StatusCode);
                    Console.WriteLine("HTTP header : {0}",response.Headers.ToString());
                    if (response.StatusCode == HttpStatusCode.Found)
                    {
                        return response.Headers.GetValues("Location").FirstOrDefault();
                    }
                    else
                    {
                        return null;
                    }                    
                }
            }
            catch (HttpRequestException hre)
            {
                Console.WriteLine("Exception : {0}", hre.Message);
                return null;
            }
        }
    }
}
