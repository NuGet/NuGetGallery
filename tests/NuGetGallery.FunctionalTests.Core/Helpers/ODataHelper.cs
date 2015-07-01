// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests
{
    /// <summary>
    /// This class has the helper methods to do gallery operations via OData.
    /// </summary>
    public class ODataHelper
        : HelperBase
    {
        public ODataHelper()
            : this(ConsoleTestOutputHelper.New)
        {
        }

        public ODataHelper(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        public Task<string> DownloadPackageFromFeed(string packageId, string version, string operation = "Install")
        {
            var client = new HttpClient();
            string requestUri = UrlHelper.V2FeedRootUrl + @"Package/" + packageId + @"/" + version;

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("user-agent", "TestAgent");
            request.Headers.Add("NuGet-Operation", operation);
            Task<HttpResponseMessage> responseTask = client.SendAsync(request);

            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();
            responseTask.ContinueWith(rt =>
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

        public async Task<string> TryDownloadPackageFromFeed(string packageId, string version)
        {
            try
            {
                var handler = new HttpClientHandler();
                handler.AllowAutoRedirect = false;
                using (var client = new HttpClient(handler))
                {
                    string requestUri = UrlHelper.V2FeedRootUrl + @"Package/" + packageId + @"/" + version;
                    var response = await client.GetAsync(requestUri);
                    //print the header
                    WriteLine("HTTP status code : {0}", response.StatusCode);
                    WriteLine("HTTP header : {0}", response.Headers);
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
                WriteLine("Exception : {0}", hre.Message);
                return null;
            }
        }

        public async Task<bool> ContainsResponseText(string url, params string[] expectedTexts)
        {
            var request = WebRequest.Create(url);
            var response = await request.GetResponseAsync();

            string responseText;
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
            }

            foreach (string s in expectedTexts)
            {
                if (!responseText.Contains(s))
                {
                    WriteLine("Response text does not contain expected text of " + s);
                    return false;
                }
            }
            return true;
        }

        public async Task<bool> ContainsResponseTextIgnoreCase(string url, params string[] expectedTexts)
        {
            var request = WebRequest.Create(url);
            var response = await request.GetResponseAsync();

            string responseText;
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = (await sr.ReadToEndAsync()).ToLowerInvariant();
            }

            foreach (string s in expectedTexts)
            {
                if (!responseText.Contains(s.ToLowerInvariant()))
                {
                    WriteLine("Response text does not contain expected text of " + s);
                    return false;
                }
            }
            return true;
        }

        public async Task DownloadPackageFromV2FeedWithOperation(string packageId, string version, string operation)
        {
            string filename = await DownloadPackageFromFeed(packageId, version, operation);

            //check if the file exists.
            Assert.True(File.Exists(filename), Constants.PackageDownloadFailureMessage);
            var clientSdkHelper = new ClientSdkHelper(TestOutputHelper);
            string downloadedPackageId = clientSdkHelper.GetPackageIdFromNupkgFile(filename);
            //Check that the downloaded Nupkg file is not corrupt and it indeed corresponds to the package which we were trying to download.
            Assert.True(downloadedPackageId.Equals(packageId), Constants.UnableToZipError);
        }
    }
}