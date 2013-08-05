using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;

namespace NuGetGallery.Monitoring.Azure
{
    public abstract class AzureStorageMonitorBase : ApplicationMonitor
    {
        private CloudStorageAccount _account = null;

        public string AccountName { get; private set; }
        public string SharedAccessSignature { get; private set; }
        public CloudStorageAccount Account { get { return _account ?? (_account = CreateAccount()); } }

        public Uri BlobEndpoint { get; private set; }
        public Uri TableEndpoint { get; private set; }
        public Uri QueueEndpoint { get; private set; }

        protected AzureStorageMonitorBase(string accountName, bool useHttps) {
            AccountName = accountName;
            BlobEndpoint = GetFullEndpoint(AccountName, "blob", useHttps);
            QueueEndpoint = GetFullEndpoint(AccountName, "queue", useHttps);
            TableEndpoint = GetFullEndpoint(AccountName, "table", useHttps);
        }
        
        protected AzureStorageMonitorBase(string accountName, string sharedAccessSignature, bool useHttps) : this(accountName, useHttps)
        {
            SharedAccessSignature = sharedAccessSignature;
        }

        private Uri GetFullEndpoint(string accountName, string type, bool useHttps)
        {
            return new Uri(String.Format("{0}://{1}.{2}.core.windows.net", useHttps ? "https" : "http", accountName, type));
        }

        private CloudStorageAccount CreateAccount()
        {
            Debug.Assert(!String.IsNullOrEmpty(SharedAccessSignature), "A shared access signature must be provided in the constructor if the monitor is going to use the Account property");
            if (String.IsNullOrEmpty(SharedAccessSignature))
            {
                throw new InvalidOperationException("A shared access signature must be provided in the constructor if the monitor is going to use the Account property");
            }
            return new CloudStorageAccount(
                new StorageCredentials(SharedAccessSignature),
                BlobEndpoint,
                QueueEndpoint,
                TableEndpoint);
        }
    }
}
