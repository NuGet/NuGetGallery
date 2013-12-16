using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Configuration;
using System.Collections;
using System.Collections.Specialized;

namespace NuGetDashboard.Utilities
{

    #region PublicMethods
    /// <summary>
    /// This class provides the service methods to load/process report blobs from Storage.
    /// </summary>
    public class BlobStorageService
    {
        private static string _connectionString = ConfigurationManager.AppSettings["StorageConnection"];

        public static string Load(string name,string containerName="dashboard")
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(_connectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(name);            
            string content;
            using (var memoryStream = new MemoryStream())
            {
                blob.DownloadToStream(memoryStream);
                content = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
            }

            return content;
        }

        /// <summary>
        /// Checks if blob by specified name exits
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static bool IfBlobExists(string name,out DateTime createdTime,string containerName="dashboard")
        {
            createdTime = new DateTime(1, 1, 1);
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(_connectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);            
            CloudBlockBlob blob = container.GetBlockBlobReference(name);
            if (blob.Exists())
            {
                blob.FetchAttributes();
                if (blob.Properties.LastModified.HasValue)
                    createdTime = blob.Properties.LastModified.Value.DateTime;
                return true;
            }
            else
                return false;
            
        }

        /// <summary>
        /// Returns the content of the latest blob in the specified container.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string DownloadLatest(string containerName = "dashboard")
        {
           // createdTime = new DateTime(1, 1, 1);
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(_connectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            CloudBlockBlob recentblob = null;
            IEnumerable<IListBlobItem> blobs = container.ListBlobs(null, false, BlobListingDetails.Metadata, null, null);
            DateTime recentTime = new DateTime(1, 1, 1);   
            //Get the blob with the latest LastModifiedDate.
            foreach (var item in blobs)
            {
                if (item.GetType() == typeof(CloudBlockBlob))
                {
                    CloudBlockBlob blob = (CloudBlockBlob)item;
                    blob.FetchAttributes();
                    if (blob.Properties.LastModified.Value.DateTime > recentTime)
                    {
                        recentTime = blob.Properties.LastModified.Value.DateTime;
                        recentblob = blob;

                    }
                }
            }
            //Return the memory stream.
            string content;
            using (var memoryStream = new MemoryStream())
            {
                recentblob.DownloadToStream(memoryStream);
                content = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
            }
            return content;
        }


        /// <summary>
        /// Gets the JSON data from the blob. The blobs are pre-created as key value pairs using Ops tasks.
        /// </summary>
        /// <param name="blobName"></param>
        /// <param name="xValues"></param>
        /// <param name="yValues"></param>
        public static void GetJsonDataFromBlob(string blobName, out List<string> xValues, out List<Object> yValues)
        {
            xValues = new List<string>();
            yValues = new List<Object>();
            string json = Load(blobName);
            if (json == null)
            {
                return;
            }

            JArray array = JArray.Parse(json);
            foreach (JObject item in array)
            {
                xValues.Add(item["key"].ToString());
                yValues.Add((item["value"]).ToString());
            }

        }

        /// <summary>
        /// Gets the JSON data from the blob. The blobs are pre-created as key value pairs using Ops tasks.
        /// </summary>
        /// <param name="blobName"></param>
        /// <param name="xValues"></param>
        /// <param name="yValues"></param>
        public static string GetValueFromBlob(string blobName, string key)
        {

            string json = Load(blobName);
            if (json == null)
            {
                return null;
            }

            JArray array = JArray.Parse(json);
            foreach (JObject item in array)
            {
                if (item["key"].ToString() == key)
                    return (item["value"].ToString());
            }

            return null;
        }


        /// <summary>
        /// Gets the JSON data from the blob. The blobs are pre-created as key value pairs using Ops tasks.
        /// </summary>
        /// <param name="blobName"></param>
        /// <param name="xValues"></param>
        /// <param name="yValues"></param>
        public static Dictionary<string, string> GetDictFromBlob(string blobName)
        {

            string json = Load(blobName);
            if (json == null)
            {
                return null;
            }

            Dictionary<string, string> dict = new Dictionary<string, string>();
            JArray array = JArray.Parse(json);
            foreach (JObject item in array)
            {
                dict.Add(item["key"].ToString(), item["value"].ToString());
            }
            return dict;
        }

        #endregion PublicMethods

        #region PublicProperty

        #endregion PublicProperty
    }
}