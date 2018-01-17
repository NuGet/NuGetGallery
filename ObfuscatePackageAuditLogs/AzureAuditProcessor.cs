
#define parallelExecution
#define overwrite

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;

using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using NuGetGallery.Auditing;


namespace ObfuscateAuditLogs
{
    public class PackageAuditEntry
    {
        public PackageAuditRecord Record { get; set; }
        public AuditActor Actor { get; set; }

        public PackageAuditEntry()
        {

        }

        public PackageAuditEntry(PackageAuditRecord record, AuditActor actor)
        {
            Record = record;
            Actor = actor;
        }
    }

    public class AzureAuditProcessor//: IProcessor
    {
        private CloudStorageAccount _storageAccountFrom;
        private CloudStorageAccount _storageAccountTo;
        private string _tempLogFolder = "0_NuGetAuditLogs";
        //private string _tempFolderPath;
        private string _tempLogFolderPath;
        private CloudBlobContainer _containerFrom;
        private CloudBlobContainer _containerTo;
        private string _sqlLogConnectionString;
        ILog _sqlLogger;
        ILog _fileLog;
        string _run;
        CloudAuditingService _service;
        DateTimeOffset? _maxDateToUpate = null;

        public DateTimeOffset MaxDateToUpate
        {
            set
            {
                _maxDateToUpate = value;
            }
        }


        public AzureAuditProcessor(string connectionStringFrom, string containerFrom, string connectionStringTo, string containerTo, string executionRunId)
        {
            _storageAccountFrom = CloudStorageAccount.Parse(connectionStringFrom);
            _storageAccountTo = CloudStorageAccount.Parse(connectionStringTo);
            _sqlLogConnectionString = ConfigurationManager.AppSettings["LogSqlServer"];

            _tempLogFolderPath = Path.Combine(Path.GetTempPath(), _tempLogFolder);
            Directory.CreateDirectory(_tempLogFolderPath);
            _containerFrom = _storageAccountFrom.CreateCloudBlobClient().GetContainerReference(containerFrom);
            _containerTo = _storageAccountTo.CreateCloudBlobClient().GetContainerReference(containerTo);
            _sqlLogger = new SQLLog(_sqlLogConnectionString);
            _fileLog = new FileLog(_tempLogFolderPath);
            // initialization does not matter 
            // it will be used only the Render method
            _service = new CloudAuditingService("", "", _containerFrom, null);
            _run = executionRunId;
        }

        public bool TryProcessFolder(string relativeFolderAddress, CancellationToken token)
        {
            var cloudDirectoryFrom = _containerFrom.GetDirectoryReference(relativeFolderAddress);
            var cloudDirectoryTo = _containerTo.GetDirectoryReference(relativeFolderAddress);
            ParallelOptions options = new ParallelOptions() { CancellationToken = token, MaxDegreeOfParallelism = 4 };
            Stopwatch sw = new Stopwatch();
            int processedFileIndex = 0;
            try
            {
                var blobs = cloudDirectoryFrom.ListBlobs(useFlatBlobListing: true);
                sw.Start();

# if parallelExecution
                Parallel.ForEach(blobs, (blob) =>
                {
                    var currentBlockBlobFrom = ((CloudBlockBlob)blob);
#if overwrite
                    ProcessBlob(currentBlockBlobFrom, cloudDirectoryFrom);
#else
                    ProcessBlob(currentBlockBlobFrom, cloudDirectoryFrom, cloudDirectoryTo);
#endif
                    Interlocked.Increment(ref processedFileIndex);
                });
#else
                foreach (var blob in blobs)
                {
                    var currentBlockBlobFrom = ((CloudBlockBlob)blob);
                    ProcessBlob(currentBlockBlobFrom, cloudDirectoryFrom, cloudDirectoryTo);
                    processedFileIndex++;
                }
#endif
                sw.Stop();

                LogRunStatusToFile(LogStatus.Pass, processedFileIndex, sw.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                sw.Stop();
                string m = e.Message;
                LogRunStatusToFile(LogStatus.Fail, processedFileIndex, sw.ElapsedMilliseconds, e);
                return false;
            }
            return true;
        }

        private void LogToSql(CloudBlockBlob blob, LogStatus status, Exception e = null)
        {
            string message = status == LogStatus.Fail ? $"{blob.Uri} failed to be processed.{GetMessageFromException(e)}" : $"{blob.Uri} was processed.";
            var data = new LogData(status, _run, message)
            {
                Operation = blob.Uri.ToString() 
            };
            _sqlLogger.LogAsync(data).Wait();
        }

        private void LogRunStatusToFile(LogStatus status, int processedFileCount, long processingTimeInMilliSeconds, Exception e = null)
        {
            string message = $"ProcessedFileCount:{processedFileCount}, ProcessingTimeInMilliSeconds:{processingTimeInMilliSeconds} \n";
            message += GetMessageFromException(e);

            var data = new LogData(status, _run, message)
            {
                Operation = "Full execution",
            };
            _fileLog.LogAsync(data).Wait();
        }

        private void ProcessBlob(CloudBlockBlob currentBlockBlobFrom, CloudBlobDirectory cloudDirectoryFrom, CloudBlobDirectory cloudDirectoryTo)
        {
            try
            {
                var absolutePath = currentBlockBlobFrom.Uri.AbsolutePath;
                var aa = Encoding.Unicode.GetBytes(absolutePath);
                var bb = Encoding.Unicode.GetString(aa);
                var cloudBlobDirFromAbsolutePath = cloudDirectoryFrom.Uri.AbsolutePath;
                CloudBlockBlob currentBlockBlobTo = cloudDirectoryTo.GetBlockBlobReference(absolutePath.Remove(0, cloudBlobDirFromAbsolutePath.Length));
                if (BlobNeedsProcessing(currentBlockBlobFrom, currentBlockBlobTo))
                {
                    using (var stream = new MemoryStream())
                    {
                        currentBlockBlobFrom.DownloadToStream(stream);
                        stream.Position = 0;//resetting stream's position to 0
                        var serializer = new JsonSerializer();

                        using (var sr = new StreamReader(stream))
                        {
                            using (var jsonTextReader = new JsonTextReader(sr))
                            {
                                try
                                {
                                    var result = serializer.Deserialize(jsonTextReader, typeof(PackageAuditEntry));
                                    var resultAuditEntry = result as PackageAuditEntry;
                                    var auditEntry = new AuditEntry(resultAuditEntry.Record, resultAuditEntry.Actor);
                                    var obfuscatedValue = _service.RenderAuditEntry(auditEntry);
                                    currentBlockBlobTo.UploadTextAsync(obfuscatedValue).Wait();
                                    LogToSql(currentBlockBlobFrom, LogStatus.Pass, null);
                                }
                                catch (Exception ex)
                                {
                                    LogToSql(currentBlockBlobFrom, LogStatus.Fail, ex);
                                }
                            }
                        }
                    }
                }
            }
            catch(Exception exception)
            {
                LogToSql(currentBlockBlobFrom, LogStatus.Fail, exception);
            }
        }

        private void ProcessBlob(CloudBlockBlob currentBlockBlobFrom, CloudBlobDirectory cloudDirectoryFrom)
        {
            try
            {
                var absolutePath = currentBlockBlobFrom.Uri.AbsolutePath;
                var aa = Encoding.Unicode.GetBytes(absolutePath);
                var bb = Encoding.Unicode.GetString(aa);
                var cloudBlobDirFromAbsolutePath = cloudDirectoryFrom.Uri.AbsolutePath;
                if (BlobNeedsProcessing(currentBlockBlobFrom, currentBlockBlobFrom))
                {
                    using (var stream = new MemoryStream())
                    {
                        currentBlockBlobFrom.DownloadToStream(stream);
                        stream.Position = 0;//resetting stream's position to 0
                        var serializer = new JsonSerializer();

                        using (var sr = new StreamReader(stream))
                        {
                            using (var jsonTextReader = new JsonTextReader(sr))
                            {
                                try
                                {
                                    var result = serializer.Deserialize(jsonTextReader, typeof(PackageAuditEntry));
                                    var resultAuditEntry = result as PackageAuditEntry;
                                    var auditEntry = new AuditEntry(resultAuditEntry.Record, resultAuditEntry.Actor);
                                    var obfuscatedValue = _service.RenderAuditEntry(auditEntry);
                                    currentBlockBlobFrom.UploadTextAsync(obfuscatedValue).Wait();
                                    LogToSql(currentBlockBlobFrom, LogStatus.Pass, null);
                                }
                                catch (Exception ex)
                                {
                                    LogToSql(currentBlockBlobFrom, LogStatus.Fail, ex);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                LogToSql(currentBlockBlobFrom, LogStatus.Fail, exception);
            }
        }

        private string GetMessageFromException(Exception ex)
        {
            if(ex==null)
            {
                return string.Empty;
            }
            var innerExMesssage = ex.InnerException != null ? ex.InnerException.Message : string.Empty;

            var message = ex.Message +"_" + innerExMesssage;
            if( ex is AggregateException)
            {
                var flatEx = ((AggregateException)ex).Flatten();
                var innerM = flatEx.InnerException != null ? flatEx.InnerException.Message : string.Empty;
                message = flatEx.Message +"_" + innerM;
            }
            string result = message.Replace("'", "").Replace("\"", "");
            return result;
        }

        private bool BlobNeedsProcessing(CloudBlockBlob blobFrom, CloudBlockBlob blobTo)
        {
            blobFrom.FetchAttributes();
            var blobUpdateTime = blobFrom.Properties.LastModified;
            bool blobCreatedTimeValidation = true;
            if (blobUpdateTime.HasValue && _maxDateToUpate.HasValue)
            {
                blobCreatedTimeValidation = blobUpdateTime.Value.ToUniversalTime() < _maxDateToUpate.Value.ToUniversalTime();
            }
            if (!blobCreatedTimeValidation)
            {
                return false;
            }
#if !overwrite
            return !blobTo.Exists();
#else
            return true;
#endif
        }

        private List<string> DirectoryBlobDifference(CloudBlobDirectory directoryFrom, CloudBlobDirectory directoryTo)
        {
            var fromBlobPaths = directoryFrom.ListBlobs(useFlatBlobListing:true).Select(b => b.Uri.AbsolutePath);
            var toBlobPaths = directoryTo.ListBlobs(useFlatBlobListing:true).Select(b => b.Uri.AbsolutePath);
            return fromBlobPaths.AsParallel().Where(b => !toBlobPaths.Contains(b)).ToList();
        }

        public void PrintDifferenceBetweenFolders(string relativeFolder)
        {
            var fromDirectory = _containerFrom.GetDirectoryReference(relativeFolder);
            var toDirectory = _containerTo.GetDirectoryReference(relativeFolder);

            var difference = DirectoryBlobDifference(fromDirectory, toDirectory);

            var message = string.Join("\r\n", difference);
            var data = new LogData(LogStatus.Info, _run, message);
            _fileLog.LogAsync(data);
        }


        /// <summary>
        /// Returns the count of blobs that will be processed 
        /// </summary>
        /// <param name="relativeFolder"></param>
        public void PrintCountOfFilesToBeProccesed(string relativeFolder)
        {
            var fromDirectory = _containerFrom.GetDirectoryReference(relativeFolder);
            int count = fromDirectory.ListBlobs(useFlatBlobListing: true)
                .Where((b) =>
               {
                   var blockBlob = (CloudBlockBlob)b;
                   blockBlob.FetchAttributes();
                   var blobUpdateTime = blockBlob.Properties.LastModified;
                   if (blobUpdateTime.HasValue && _maxDateToUpate.HasValue)
                   {
                       return blobUpdateTime.Value.ToUniversalTime() < _maxDateToUpate.Value.ToUniversalTime();
                   }
                   return true;
               }).Count();

            int totalCount = fromDirectory.ListBlobs(useFlatBlobListing: true).Count();
             var data = new LogData(LogStatus.Info, _run, $"Count of files to be processed:{count}. Total count:{totalCount}");
            _fileLog.LogAsync(data);
        }
    }
}
