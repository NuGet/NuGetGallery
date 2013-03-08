using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGetGallery.FunctionTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NuGetGallery.FunctionalTests.ODataFeedTests
{
    /// <summary>
    /// Checks if the basic operations against V2 Feed works fine.
    /// </summary>
    [TestClass]
    public partial class V2FeedTest
    {
        [TestMethod]
        [Description("Downloads a package from the V2 feed and checks if the file is present on local disk")]
        [Priority(0)]
        public void DownloadPackageFromV2Feed()
        {
            ClientSDKHelper.ClearMachineCache(); //clear local cache.
            try
            {
                string packageId = Constants.TestPackageId; //try to down load a pre-defined test package.   
                string version = "1.0.0";
                Task<string> downloadTask = DownloadPackage(packageId, version);                
                string filename = downloadTask.Result;
                //check if the file exists.
                Assert.IsTrue(File.Exists(filename), " Package download from V2 feed didnt work");
                string downloadedPackageId = ClientSDKHelper.GetPackageIdFromNupkgFile(filename);
                //Check that the downloaded Nupkg file is not corrupt and it indeed corresponds to the package which we were trying to download.
                Assert.IsTrue(downloadedPackageId.Equals(packageId), "Unable to unzip the package downloaded via V2 feed. Check log for details");
                
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }
        }

        [TestMethod]
        [Description("Restores a package from the V2 feed and checks if the file is present on local disk")]
        [Priority(0)]
        public void RestorePackageFromV2Feed()
        {
            ClientSDKHelper.ClearMachineCache(); //clear local cache.
            try
            {
                string packageId = Constants.TestPackageId; //the package name shall be fixed as it really doesnt matter which package we are trying to install.
                string version = "1.0.0";
                Task<string> downloadTask = DownloadPackage(packageId, version,"Restore");
                string filename = downloadTask.Result;
                //check if the file exists.
                Assert.IsTrue(File.Exists(filename), " Package restore from V2 feed didnt work");
                string downloadedPackageId = ClientSDKHelper.GetPackageIdFromNupkgFile(filename);
                //Check that the downloaded Nupkg file is not corrupt and it indeed corresponds to the package which we were trying to download.
                Assert.IsTrue(downloadedPackageId.Equals(packageId), "Unable to unzip the package restored via V2 Feed. Check log for details");

            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }
        }

        #region PrivateMethods
        private Task<string> DownloadPackage(string packageId, string version,string operation="Install")
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
            #endregion PrivateMethods
        }
    }
}
