using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring.Azure
{
    public class BlobDownloadTimeMonitor : AzureBlobMonitorBase
    {
        public BlobDownloadTimeMonitor(string blobPath, string accountName, bool useHttps) : base(blobPath, accountName, useHttps) { }

        protected override async Task Invoke()
        {
            // Download the blob
            string tempFile = Path.GetTempFileName();
            Stopwatch sw = new Stopwatch();
            try
            {
                sw.Start();
                await new WebClient().DownloadFileTaskAsync(BlobUrl.AbsoluteUri, tempFile);
                sw.Stop();

                Success(String.Format("Successfully downloaded {0}", BlobPath));
                QoS(String.Format("Download {0}", BlobPath), success: true, timeTaken: sw.Elapsed);
            }
            catch (Exception ex)
            {
                Failure(String.Format("Download Failure: {0}", ex.GetBaseException().Message));
            }
            finally
            {
                if (sw.IsRunning)
                {
                    sw.Stop();
                }
                if (File.Exists(tempFile))
                {
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch (Exception)
                    {
                        // If we fail to delete the temp file... whatever.
                    }
                }
            }
        }
    }
}
