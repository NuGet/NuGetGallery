
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
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using NuGetGallery.Auditing;


namespace ObfuscateAuditLogs
{
    public class PackageAuditEntry2
    {
        public PackageAuditRecord2 Record { get; set; }
        public AuditActor Actor { get; set; }

        public PackageAuditEntry2()
        {

        }

        public PackageAuditEntry2(PackageAuditRecord2 record, AuditActor actor)
        {
            Record = record;
            Actor = actor;
        }
    }

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

        //if some of the blobs need to be ignored add the to this collection
        private HashSet<string> _blobExclusionList ;


        public DateTimeOffset MaxDateToUpate
        {
            set
            {
                _maxDateToUpate = value;
            }
        }

        public AzureAuditProcessor(string connectionStringFrom)
        {
            _service = new CloudAuditingService("", "", _containerFrom, null);
        }

        public AzureAuditProcessor(string connectionStringFrom, string containerFrom, string connectionStringTo, string containerTo, string executionRunId, HashSet<string> blobExclusionList)
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
            _blobExclusionList = new HashSet<string>(blobExclusionList);
        }

        public AzureAuditProcessor(string connectionStringFrom, string containerFrom, string connectionStringTo, string containerTo, string executionRunId) 
            : this(connectionStringFrom, containerFrom, connectionStringTo, containerTo, executionRunId, new HashSet<string>())
        {
            
        }

        public string RunId
        {
            get
            {
                return _run;
            }
            set
            {
                _run = value;
            }
        }

//        public bool TryProcessFolder(string relativeFolderAddress, CancellationToken token)
//        {
//            var cloudDirectoryFrom = _containerFrom.GetDirectoryReference(relativeFolderAddress);
//            var cloudDirectoryTo = _containerTo.GetDirectoryReference(relativeFolderAddress);
//            ParallelOptions options = new ParallelOptions() { CancellationToken = token, MaxDegreeOfParallelism = 2 };
//            Stopwatch sw = new Stopwatch();
//            int processedFileIndex = 0;
//            try
//            {
//                var blobs = cloudDirectoryFrom.ListBlobs(useFlatBlobListing: true);
//                sw.Start();

//# if parallelExecution
//                Parallel.ForEach(blobs, (blob) =>
//                {
//                    var currentBlockBlobFrom = ((CloudBlockBlob)blob);
//#if overwrite
//                    ProcessBlob(currentBlockBlobFrom);
//#else
//                    ProcessBlob(currentBlockBlobFrom, cloudDirectoryFrom, cloudDirectoryTo);
//#endif
//                    Interlocked.Increment(ref processedFileIndex);
//                });
//#else
//                foreach (var blob in blobs)
//                {
//                    var currentBlockBlobFrom = ((CloudBlockBlob)blob);
//                    ProcessBlob(currentBlockBlobFrom, cloudDirectoryFrom, cloudDirectoryTo);
//                    processedFileIndex++;
//                }
//#endif
//                sw.Stop();

//                LogRunStatusToFile(LogStatus.Pass, processedFileIndex, sw.ElapsedMilliseconds);
//            }
//            catch (Exception e)
//            {
//                sw.Stop();
//                string m = e.Message;
//                LogRunStatusToFile(LogStatus.Fail, processedFileIndex, sw.ElapsedMilliseconds, e);
//                return false;
//            }
//            return true;
//        }

        public bool TryProcessFolderSegmented(string relativeFolderAddress, CancellationToken token)
        {
            var cloudDirectoryFrom = _containerFrom.GetDirectoryReference(relativeFolderAddress);
            var cloudDirectoryTo = _containerTo.GetDirectoryReference(relativeFolderAddress);
            Stopwatch sw = new Stopwatch();
            int processedFileIndex = 0;
            try
            {
                BlobContinuationToken bctoken = null;
                sw.Start();

                do
                {
                    var result = cloudDirectoryFrom.ListBlobsSegmented(
                        useFlatBlobListing: true,
                        blobListingDetails: BlobListingDetails.Metadata,
                        maxResults: null,
                        currentToken: bctoken, options: null,
                        operationContext: null);
                    bctoken = result.ContinuationToken;
                    var blobs = result.Results;
#if parallelExecution
                    ParallelOptions options = new ParallelOptions() { CancellationToken = token, MaxDegreeOfParallelism = 2 };

                    Parallel.ForEach(blobs, (blob) =>
                    {
                        var currentBlockBlobFrom = ((CloudBlockBlob)blob);
#if overwrite
                        ProcessBlob(currentBlockBlobFrom);
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
                    Console.WriteLine($"Blobs:{processedFileIndex}");

                } while (bctoken != null);

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

        public bool TryProcessListSegmented(IEnumerable<string> blobUriList, CancellationToken token)
        {
            Stopwatch sw = new Stopwatch();
            int processedFileIndex = 0;
            StorageCredentials credFrom = _storageAccountFrom.Credentials;
            try
            {
                sw.Start();

                var blobs = blobUriList.Select(b => new CloudBlockBlob(new Uri(b), credFrom));
                foreach (var blob in blobs)
                {
                    var currentBlockBlobFrom = ((CloudBlockBlob)blob);
                    ProcessBlob_PackageAuditEntry2(currentBlockBlobFrom);
                    processedFileIndex++;
                }

                Console.WriteLine($"Blobs:{processedFileIndex}");

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
            message += $"{e.GetType()} \n" + GetMessageFromException(e);

            var data = new LogData(status, _run, message)
            {
                Operation = "Full execution",
            };
            _fileLog.LogAsync(data).Wait();
        }

        private void ProcessBlob2(CloudBlockBlob currentBlockBlobFrom, CloudBlobDirectory cloudDirectoryFrom, CloudBlobDirectory cloudDirectoryTo)
        {
            try
            {
                var absolutePath = currentBlockBlobFrom.Uri.AbsolutePath;
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

        private void ProcessBlob(CloudBlockBlob currentBlockBlob)
        {
            try
            {
                if(BlobNeedsProcessing(currentBlockBlob, currentBlockBlob))
                {
                    using (var stream = new MemoryStream())
                    {
                        currentBlockBlob.DownloadToStream(stream);
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
                                    currentBlockBlob.UploadTextAsync(obfuscatedValue).Wait();
                                    LogToSql(currentBlockBlob, LogStatus.Pass, null);
                                }
                                catch (Exception ex)
                                {
                                    LogToSql(currentBlockBlob, LogStatus.Fail, ex);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                LogToSql(currentBlockBlob, LogStatus.Fail, exception);
            }
        }

        private void ProcessBlob_PackageAuditEntry2(CloudBlockBlob currentBlockBlob)
        {
            try
            {
                if (BlobNeedsProcessing(currentBlockBlob, currentBlockBlob))
                {
                    using (var stream = new MemoryStream())
                    {
                        currentBlockBlob.DownloadToStream(stream);
                        stream.Position = 0;//resetting stream's position to 0
                        var serializer = new JsonSerializer();

                        using (var sr = new StreamReader(stream))
                        {
                            using (var jsonTextReader = new JsonTextReader(sr))
                            {
                                try
                                {
                                    var result = serializer.Deserialize(jsonTextReader, typeof(PackageAuditEntry2));
                                    var resultAuditEntry2 = result as PackageAuditEntry2;

                                    var resultAuditEntry = ConvertFrom(resultAuditEntry2);
                                    var auditEntry = new AuditEntry(resultAuditEntry.Record, resultAuditEntry.Actor);
                                    var obfuscatedValue = _service.RenderAuditEntry(auditEntry);
                                    currentBlockBlob.UploadTextAsync(obfuscatedValue).Wait();
                                    LogToSql(currentBlockBlob, LogStatus.Pass, null);
                                }
                                catch (Exception ex)
                                {
                                    LogToSql(currentBlockBlob, LogStatus.Fail, ex);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                LogToSql(currentBlockBlob, LogStatus.Fail, exception);
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
            if(_blobExclusionList.Contains(blobFrom.Uri.ToString()))
            {
                //Console.Write(".");
                return false;
            }
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
        public void LogFilesToBeProccesed(string relativeFolder)
        {
            Stopwatch sw = new Stopwatch();
            int processedFileIndex = 0;
            var fromDirectory = _containerFrom.GetDirectoryReference(relativeFolder);
            BlobContinuationToken bctoken = null;
            sw.Start();
            do
            {
                Console.WriteLine(processedFileIndex);
                var result = fromDirectory.ListBlobsSegmented(
                    useFlatBlobListing: true,
                    blobListingDetails: BlobListingDetails.Metadata,
                    maxResults: null,
                    currentToken: bctoken, options: null,
                    operationContext: null);
                bctoken = result.ContinuationToken;
                var blobs = result.Results;

                ParallelOptions options = new ParallelOptions() { MaxDegreeOfParallelism = 2 };

                Parallel.ForEach(blobs, (blob) =>
                {
                    var currentBlockBlobFrom = ((CloudBlockBlob)blob);
                    if(BlobNeedsProcessing(currentBlockBlobFrom, currentBlockBlobFrom))
                    {
                        LogToSql(currentBlockBlobFrom, LogStatus.Pass);
                    }
                    Interlocked.Increment(ref processedFileIndex);
                });
            } while (bctoken != null);
            sw.Stop();
            LogRunStatusToFile(LogStatus.Pass, processedFileIndex, sw.ElapsedMilliseconds);
        }

        public void TestDeserialization(string localFileName)
        {
            //using (var stream = new MemoryStream())
            //{
                using (var fs = File.OpenRead(localFileName))
                {
                    //stream.SetLength(fs.Length);
                    //fs.Read()
                    //stream.Position = 0;//resetting stream's position to 0
                    var serializer = new JsonSerializer();

                    using (var sr = new StreamReader(fs))
                    {
                        using (var jsonTextReader = new JsonTextReader(sr))
                        {
                            try
                            {
                                var result = serializer.Deserialize(jsonTextReader, typeof(PackageAuditEntry2));
                                var resultAuditEntry2 = result as PackageAuditEntry2;

                                var resultAuditEntry = ConvertFrom(resultAuditEntry2);

                                var auditEntry = new AuditEntry(resultAuditEntry.Record, resultAuditEntry.Actor);
                                var obfuscatedValue = _service.RenderAuditEntry(auditEntry);
                                
                            }
                            catch (Exception ex)
                            {
                            Console.WriteLine(ex);
                            }
                        }
                    }
                }
            //}
        }

        PackageAuditEntry ConvertFrom(PackageAuditEntry2 entry)
        {
            PackageAuditEntry result = new PackageAuditEntry();

            result.Actor = entry.Actor;
            var record = new PackageAuditRecord();
            if (entry.Record.Action == AuditedPackageAction.Deleted)
            {
                record.Action = AuditedPackageAction.Delete;
            }
            else if (entry.Record.Action == AuditedPackageAction.SoftDeleted)
            {
                record.Action = AuditedPackageAction.SoftDelete;
            }
            else
            {
                record.Action = entry.Record.Action;
            }
            record.Hash = entry.Record.Hash;
            record.Id = entry.Record.Id;
            record.PackageRecord = entry.Record.PackageRecord[0];
            record.RegistrationRecord = entry.Record.RegistrationRecord[0];
            record.Version = entry.Record.Version;
            record.Reason = entry.Record.Reason;

            result.Record = record;
            return result;
        }


    }
}
