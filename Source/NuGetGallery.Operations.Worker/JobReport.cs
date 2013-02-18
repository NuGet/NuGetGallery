using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;

namespace NuGetGallery.Operations.Worker
{
    class JobReport
    {
        // The lock is used to protect the remote blob. The blob is read and re-written within the scope of
        // this lock. As we currently only have a single client for the blob this is sufficient. This is a
        // specific minimal solution for this scenario. (A more advanced and distributed implementation would
        // make use of blob ETags to implement an optimistic concurrency scheme. This is not required here.)
        static object LockObject = new object();

        public static void Update(Settings settings, params JobStatusReport[] reports)
        {
            lock (LockObject)
            {
                CloudBlobClient blobClient = settings.ReportStorage.CreateCloudBlobClient();
                CloudBlobContainer blobContainer = blobClient.GetContainerReference("ops");
                blobContainer.CreateIfNotExists();

                CloudBlockBlob jobsReportBlob = blobContainer.GetBlockBlobReference("jobs.json");

                //  read the current report

                JObject currentReport;

                try
                {
                    MemoryStream downloadStream = new MemoryStream();
                    jobsReportBlob.DownloadToStream(downloadStream);
                    downloadStream.Seek(0, SeekOrigin.Begin);

                    using (TextReader reader = new StreamReader(downloadStream))
                    {
                        string downloadJson = reader.ReadToEnd();
                        currentReport = (JObject)JToken.Parse(downloadJson);
                    }
                }
                catch (StorageException e)
                {
                    // we don't need to fail if the blob has been deleted
                    if (e.RequestInformation.HttpStatusCode == 404)
                    {
                        currentReport = new JObject();
                    }
                    else
                    {
                        throw;
                    }
                }

                //  update the appropriate entries

                string processId;
                try
                {
                    processId = Process.GetCurrentProcess().Id.ToString();
                }
                catch (Exception)
                {
                    processId = string.Empty;
                }

                foreach (JobStatusReport report in reports)
                {
                    JObject latestJobStatusEntry = new JObject();

                    latestJobStatusEntry["pid"] = processId;
                    latestJobStatusEntry["at"] = report.At;
                    latestJobStatusEntry["duration"] = report.Duration;
                    latestJobStatusEntry["status"] = report.Status;
                    latestJobStatusEntry["message"] = report.Message;

                    JArray jobStatus;
                    JToken tok;
                    if (currentReport.TryGetValue(report.JobName, out tok))
                    {
                        jobStatus = (JArray)tok;
                    }
                    else
                    {
                        jobStatus = new JArray();
                    }

                    if (jobStatus.Count == 7)
                    {
                        jobStatus.RemoveAt(0);
                    }
                    jobStatus.Add(latestJobStatusEntry);

                    currentReport[report.JobName] = jobStatus;
                }

                //  write the  whole thing back again

                MemoryStream uploadStream = new MemoryStream();
                TextWriter writer = new StreamWriter(uploadStream);
                string uploadJson = currentReport.ToString();
                writer.Write(uploadJson);
                writer.Flush();
                uploadStream.Seek(0, SeekOrigin.Begin);

                jobsReportBlob.Properties.ContentType = "application/json";
                jobsReportBlob.Properties.CacheControl = "no-cache, no-store, must-revalidate";
                jobsReportBlob.UploadFromStream(uploadStream);
            }
        }

        public static void Initialize(Settings settings)
        {
            lock (LockObject)
            {
                CloudBlobClient blobClient = settings.ReportStorage.CreateCloudBlobClient();
                CloudBlobContainer blobContainer = blobClient.GetContainerReference("ops");
                blobContainer.CreateIfNotExists();

                CloudBlockBlob webPageBlob = blobContainer.GetBlockBlobReference("dashboard.html");

                if (!webPageBlob.Exists())
                {
                    MemoryStream stream = new MemoryStream();
                    TextWriter writer = new StreamWriter(stream);
                    writer.Write(LoadFile("NuGetGallery.Operations.Worker.Dashboard.html"));
                    writer.Flush();
                    stream.Seek(0, SeekOrigin.Begin);

                    webPageBlob.Properties.ContentType = "text/html";
                    webPageBlob.Properties.CacheControl = "no-cache, no-store, must-revalidate";
                    webPageBlob.UploadFromStream(stream);
                }

                CloudBlockBlob jobsReportBlob = blobContainer.GetBlockBlobReference("jobs.json");

                if (!jobsReportBlob.Exists())
                {
                    MemoryStream stream = new MemoryStream();
                    TextWriter writer = new StreamWriter(stream);
                    writer.Write("{}");
                    writer.Flush();
                    stream.Seek(0, SeekOrigin.Begin);

                    jobsReportBlob.Properties.ContentType = "application/json";
                    jobsReportBlob.Properties.CacheControl = "no-cache, no-store, must-revalidate";
                    jobsReportBlob.UploadFromStream(stream);
                }
            }
        }

        private static string LoadFile(string filename)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(filename))
            {
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
