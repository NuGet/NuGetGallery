﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
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
        private static readonly XNamespace FeedAtomNS = "http://www.w3.org/2005/Atom";
        private static readonly XNamespace FeedAdoNS = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";

        public ODataHelper()
            : this(ConsoleTestOutputHelper.New)
        {
        }

        public ODataHelper(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
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
                    var response = await client.GetAsync(requestUri).ConfigureAwait(false);

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

        private object GetPackageProperty(string propertyType, string propertyValue)
        {
            switch (propertyType)
            {
                case "Edm.DateTime":
                    return DateTime.Parse(propertyValue);
                case "Edm.Boolean":
                    return Boolean.Parse(propertyValue);
                default:
                    return propertyValue;
            }
        }

        public async Task<Dictionary<string,object>> GetPackagePropertiesFromResponse(string url, string packageId, string version = "1.0.0")
        {
            WriteLine($"Getting properties for package '{packageId}' with version '{version}'.");

            var responseText = await GetResponseText(url);

            var packagesXml = XDocument.Parse(responseText);

            var entries = packagesXml.Root.Elements(FeedAtomNS + "entry").ToList();
            var packageEntry = entries.First(e => e.Element(FeedAtomNS + "id").Value.Contains($"Id='{packageId}'"));
            var packageProperties = packageEntry.Element(FeedAdoNS + "properties");

            var properties = new Dictionary<string, object>();
            foreach (var prop in packageProperties.Elements())
            {
                var propType = prop.Attribute(FeedAdoNS + "type")?.Value;
                properties[prop.Name.LocalName] = GetPackageProperty(propType, prop.Value);
            }
            return properties;
        }

        public async Task<bool> ContainsResponseText(string url, params string[] expectedTexts)
        {
            var responseText = await GetResponseText(url);

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
            var responseText = (await GetResponseText(url)).ToLowerInvariant();

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

        private async Task<string> GetResponseText(string url)
        {
            var request = WebRequest.Create(url);
            using (var response = await request.GetResponseAsync())
            {
                string responseText;
                using (var sr = new StreamReader(response.GetResponseStream()))
                {
                    responseText = await sr.ReadToEndAsync();
                }

                return responseText;
            }
        }

        public async Task<WebResponse> SendRequest(string url)
        {
            var request = WebRequest.Create(url);
            return await request.GetResponseAsync().ConfigureAwait(false);
        }

        public async Task DownloadPackageFromV2FeedWithOperation(string packageId, string version, string operation)
        {
            string filename = await DownloadPackageFromFeed(packageId, version, operation);

            // Check if the file exists.
            Assert.True(File.Exists(filename), Constants.PackageDownloadFailureMessage);
            var clientSdkHelper = new ClientSdkHelper(TestOutputHelper);
            string downloadedPackageId = clientSdkHelper.GetPackageIdFromNupkgFile(filename);
            // Check that the downloaded Nupkg file is not corrupt and it indeed corresponds to the package which we were trying to download.
            Assert.True(downloadedPackageId.Equals(packageId), Constants.UnableToZipError);
        }

        private async Task<string> DownloadPackageFromFeed(string packageId, string version, string operation = "Install")
        {
            string filename;
            var client = new HttpClient();
            var requestUri = UrlHelper.V2FeedRootUrl + @"Package/" + packageId + @"/" + version;

            TestOutputHelper.WriteLine("GET " + requestUri);

            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("user-agent", "TestAgent");
            request.Headers.Add("NuGet-Operation", operation);

            var responseMessage = await client.SendAsync(request);

            if (responseMessage.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    var contentDisposition = responseMessage.Content.Headers.ContentDisposition;
                    if (contentDisposition != null)
                    {
                        filename = contentDisposition.FileName;
                    }
                    else
                    {
                        // if file name not present set the package Id for the file name
                        filename = packageId;
                    }

                    using (var fileStream = File.Create(filename))
                    {
                        await responseMessage.Content.CopyToAsync(fileStream);
                    }
                }
                catch (Exception e)
                {
                    TestOutputHelper.WriteLine("EXCEPTION: " + e);
                    throw;
                }
            }
            else
            {
                var message = string.Format("Http StatusCode: {0}", responseMessage.StatusCode);
                TestOutputHelper.WriteLine(message);
                throw new ApplicationException(message);
            }

            return filename;
        }
    }
}