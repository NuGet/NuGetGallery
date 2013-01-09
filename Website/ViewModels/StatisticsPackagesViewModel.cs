using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class StatisticsPackagesViewModel
    {
        List<StatisticsPackagesItemViewModel> _downloadPackagesSummary;
        List<StatisticsPackagesItemViewModel> _downloadPackageVersionsSummary;
        List<StatisticsPackagesItemViewModel> _downloadPackagesAll;
        List<StatisticsPackagesItemViewModel> _downloadPackageVersionsAll;
        List<StatisticsPackagesItemViewModel> _packageDownloadsByVersion;

        public StatisticsPackagesViewModel()
        {
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesSummary 
        { 
            get
            {
                if (_downloadPackagesSummary == null)
                {
                    _downloadPackagesSummary = new List<StatisticsPackagesItemViewModel>();
                }
                return _downloadPackagesSummary;
            }
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsSummary
        { 
            get
            {
                if (_downloadPackageVersionsSummary == null)
                {
                    _downloadPackageVersionsSummary = new List<StatisticsPackagesItemViewModel>();
                }
                return _downloadPackageVersionsSummary;
            }
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesAll
        {
            get
            {
                if (_downloadPackagesAll == null)
                {
                    _downloadPackagesAll = new List<StatisticsPackagesItemViewModel>();
                }
                return _downloadPackagesAll;
            }
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsAll
        { 
            get
            {
                if (_downloadPackageVersionsAll == null)
                {
                    _downloadPackageVersionsAll = new List<StatisticsPackagesItemViewModel>();
                }
                return _downloadPackageVersionsAll;
            }
        }

        public IEnumerable<StatisticsPackagesItemViewModel> PackageDownloadsByVersion
        {
            get
            {
                if (_packageDownloadsByVersion == null)
                {
                    _packageDownloadsByVersion = new List<StatisticsPackagesItemViewModel>();
                }
                return _packageDownloadsByVersion;
            }
        }

        public string PackageId { get; private set; }
        public int TotalPackageDownloads { get; private set; }

        public void LoadDownloadPackages()
        {
            JArray array = LoadReport("RecentPopularity.json");

            foreach (JObject item in array)
            {
                ((List<StatisticsPackagesItemViewModel>)DownloadPackagesAll).Add(new StatisticsPackagesItemViewModel
                {
                    PackageId = item["PackageId"].ToString(),
                    Downloads = int.Parse(item["Downloads"].ToString())
                });
            }

            for (int i = 0; i < 10; i++)
            {
                ((List<StatisticsPackagesItemViewModel>)DownloadPackagesSummary).Add(((List<StatisticsPackagesItemViewModel>)DownloadPackagesAll)[i]);
            }
        }

        public void LoadDownloadPackageVersions()
        {
            JArray array = LoadReport("RecentPopularityDetail.json");

            foreach (JObject item in array)
            {
                ((List<StatisticsPackagesItemViewModel>)DownloadPackageVersionsAll).Add(new StatisticsPackagesItemViewModel
                {
                    PackageId = item["PackageId"].ToString(),
                    PackageVersion = item["PackageVersion"].ToString(),
                    Downloads = int.Parse(item["Downloads"].ToString())
                });
            }

            for (int i = 0; i < 10; i++)
            {
                ((List<StatisticsPackagesItemViewModel>)DownloadPackageVersionsSummary).Add(((List<StatisticsPackagesItemViewModel>)DownloadPackageVersionsAll)[i]);
            }
        }

        public void LoadPackageDownloadsByVersion(string id)
        {
            if (id == string.Empty)
            {
                return;
            }

            JArray array = LoadReport(string.Format("RecentPopularity_{0}.json", id));

            this.TotalPackageDownloads = 0;

            foreach (JObject item in array)
            {
                int downloads = int.Parse(item["Downloads"].ToString());

                ((List<StatisticsPackagesItemViewModel>)PackageDownloadsByVersion).Add(new StatisticsPackagesItemViewModel
                {
                    PackageVersion = item["PackageVersion"].ToString(),
                    Downloads = downloads
                });

                this.TotalPackageDownloads += downloads;
            }

            this.PackageId = id;
        }

        private JArray LoadReport(string name)
        {
            //DEBUG
            string accountName = "nugetstatspreview";
            string accountKey = "RQysIkEqC37AfwaZRtJHFRPBVGnI1I2cV7k/DSvr6S9l2nv1yCbMAXHCb0qhSWa+OQblqOkCWVbaHDo+pYBKbA==";
            string connectionString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", accountName, accountKey);
            //

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("popularity");
            CloudBlockBlob blob = container.GetBlockBlobReference(name);

            //TODO: async OpenRead

            string content;
            using (TextReader reader = new StreamReader(blob.OpenRead()))
            {
                content = reader.ReadToEnd();
            }

            return JArray.Parse(content);
        }
    }
}
