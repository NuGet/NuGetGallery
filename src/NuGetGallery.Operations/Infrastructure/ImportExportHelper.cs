// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Xml;
using System.Web;
using System.Runtime.Serialization;
using NuGetGallery.Operations.SqlDac;
using NLog;
using System.Threading;
using System.Diagnostics.CodeAnalysis;

namespace WASDImportExport
{
    class ImportExportHelper
    {
        public string EndPointUri { get; set; }
        public string StorageKey { get; set; }
        public string ServerName { get; set; }
        public string DatabaseName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }

        private Logger _log;

        public ImportExportHelper(Logger log)
        {
            _log = log;
            EndPointUri = "";
            ServerName = "";
            StorageKey = "";
            DatabaseName = "";
            UserName = "";
            Password = "";
        }

        [SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        public string DoExport(string blobUri, bool whatIf)
        {
            _log.Info("Starting SQL DAC Export Operation");
            string requestGuid = null;
            bool exportComplete = false;
            string exportedBlobPath = null;

            //Setup Web Request for Export Operation
            WebRequest webRequest = WebRequest.Create(this.EndPointUri + @"/Export");
            webRequest.Method = WebRequestMethods.Http.Post;
            webRequest.ContentType = @"application/xml";

            //Create Web Request Inputs - Blob Storage Credentials and Server Connection Info
            ExportInput exportInputs = new ExportInput
            {
                BlobCredentials = new BlobStorageAccessKeyCredentials
                {
                    StorageAccessKey = this.StorageKey,
                    Uri = String.Format(blobUri, this.DatabaseName, DateTime.UtcNow.Ticks.ToString())
                },
                ConnectionInfo = new ConnectionInfo
                {
                    ServerName = this.ServerName,
                    DatabaseName = this.DatabaseName,
                    UserName = this.UserName,
                    Password = this.Password
                }
            };

            //Perform Web Request
            DataContractSerializer dataContractSerializer = new DataContractSerializer(exportInputs.GetType());
            _log.Info("http POST {0}", webRequest.RequestUri.AbsoluteUri);
            if (whatIf)
            {
                _log.Trace("Would have sent:");

                using (var strm = new MemoryStream())
                {
                    dataContractSerializer.WriteObject(strm, exportInputs);
                    strm.Flush();
                    strm.Seek(0, SeekOrigin.Begin);
                    using (var reader = new StreamReader(strm))
                    {
                        _log.Trace(reader.ReadToEnd());
                    }
                }
                return null;
            }
            else
            {
                _log.Info("Making Web Request For Export Operation...");
                using (var webRequestStream = webRequest.GetRequestStream())
                {
                    dataContractSerializer.WriteObject(webRequestStream, exportInputs);
                }

                //Get Response and Extract Request Identifier
                try
                {
                    //Initialize the WebResponse to the response from the WebRequest
                    using (var webResponse = webRequest.GetResponse())
                    {
                        using (var xmlStreamReader = XmlReader.Create(webResponse.GetResponseStream()))
                        {
                            xmlStreamReader.ReadToFollowing("guid");
                            requestGuid = xmlStreamReader.ReadElementContentAsString();
                            _log.Info($"Export Request '{requestGuid}' submitted");

                            //Get Export Operation Status
                            string last = null;
                            while (!exportComplete)
                            {
                                List<StatusInfo> statusInfoList = CheckRequestStatus(requestGuid);
                                var status = statusInfoList.FirstOrDefault().Status;
                                if (!String.Equals(last, status, StringComparison.OrdinalIgnoreCase))
                                {
                                    _log.Info(status);
                                }
                                last = status;

                                if (statusInfoList.FirstOrDefault().Status == "Failed")
                                {
                                    _log.Error("Database export failed: {0}", statusInfoList.FirstOrDefault().ErrorMessage);
                                    exportComplete = true;
                                }

                                if (statusInfoList.FirstOrDefault().Status == "Completed")
                                {
                                    exportedBlobPath = statusInfoList.FirstOrDefault().BlobUri;
                                    _log.Info("Export Complete - Database exported to: {0}", exportedBlobPath);
                                    exportComplete = true;
                                }
                                Thread.Sleep(5 * 1000);
                            }
                            return exportedBlobPath;
                        }
                    }
                }
                catch (WebException responseException)
                {
                    _log.Error("Request Falied:{0}", responseException.Message);
                    if (responseException.Response != null)
                    {
                        _log.Error("Status Code: {0}", ((HttpWebResponse)responseException.Response).StatusCode);
                        _log.Error("Status Description: {0}", ((HttpWebResponse)responseException.Response).StatusDescription);
                    }
                    return null;
                }
            }
        }

        public List<StatusInfo> CheckRequestStatus(string requestGuid)
        {
            WebRequest webRequest = WebRequest.Create(this.EndPointUri +
                                                      $"/Status?servername={HttpUtility.UrlEncode(this.ServerName)}&username={HttpUtility.UrlEncode(this.UserName)}&password={HttpUtility.UrlEncode(this.Password)}&reqId={HttpUtility.UrlEncode(requestGuid)}");

            webRequest.Method = WebRequestMethods.Http.Get;
            webRequest.ContentType = @"application/xml";
            WebResponse webResponse = webRequest.GetResponse();
            XmlReader xmlStreamReader = XmlReader.Create(webResponse.GetResponseStream());
            DataContractSerializer dataContractSerializer = new DataContractSerializer(typeof(List<StatusInfo>));

            return (List<StatusInfo>)dataContractSerializer.ReadObject(xmlStreamReader, true);
        }
    }
}
