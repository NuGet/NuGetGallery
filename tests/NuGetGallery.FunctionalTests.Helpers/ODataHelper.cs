using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;


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

        public static bool ContainsResponseText(string url, params string[] expectedTexts)
        {
            WebRequest request = WebRequest.Create(url);
            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();

            foreach (string s in expectedTexts)
            {
                if (!responseText.Contains(s))
                {
                    Console.WriteLine("Response text does not contain expected text of " + s);
                    return false;
                }
            }
            return true;
        }

        public static bool ContainsResponseTextIgnoreCase(string url, params string[] expectedTexts)
        {
            WebRequest request = WebRequest.Create(url);
            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd().ToLowerInvariant();

            foreach (string s in expectedTexts)
            {
                if (!responseText.Contains(s.ToLowerInvariant()))
                {
                    Console.WriteLine("Response text does not contain expected text of " + s);
                    return false;
                }
            }
            return true;
        }

        public static void DownloadPackageFromV2FeedWithOperation(string packageId, string version, string operation)
        {
            try
            {
                Task<string> downloadTask = ODataHelper.DownloadPackageFromFeed(packageId, version, operation);
                string filename = downloadTask.Result;
                //check if the file exists.
                Assert.IsTrue(File.Exists(filename), Constants.PackageDownloadFailureMessage);
                string downloadedPackageId = ClientSDKHelper.GetPackageIdFromNupkgFile(filename);
                //Check that the downloaded Nupkg file is not corrupt and it indeed corresponds to the package which we were trying to download.
                Assert.IsTrue(downloadedPackageId.Equals(packageId), Constants.UnableToZipError);
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }
        }
    }
}
