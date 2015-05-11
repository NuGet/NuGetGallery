// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MennoCopy
{
    /// <summary>
    /// MennoCopy copies a blob container and rewrites URLs contained in the blobs to match the new path.
    /// </summary>
    class Program
    {
        private static bool _run = true;

        // if overwrite is false existing blobs will be skipped
        private static bool _overwrite = false;

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine(".exe <config path> <threads>");
                Environment.Exit(1);
            }

            Console.WriteLine("Overwrite: " + (_overwrite ? "true" : "false"));

            Console.CancelKeyPress += Console_CancelKeyPress;

            int threads = 8;
            Int32.TryParse(args[1], out threads);

            FileInfo file = new FileInfo(args[0]);

            if (!file.Exists)
            {
                throw new FileNotFoundException(file.FullName);
            }

            JObject config = null;

            using (var stream = file.OpenText())
            {
                config = JObject.Parse(stream.ReadToEnd());
            }

            Stopwatch runtime = new Stopwatch();
            runtime.Start();

            // set up
            CloudStorageAccount inputAccount = CloudStorageAccount.Parse(config["InputConnectionString"].ToString());
            CloudStorageAccount outputAccount = CloudStorageAccount.Parse(config["OutputConnectionString"].ToString());

            var inputClient = inputAccount.CreateCloudBlobClient();
            var outputClient = outputAccount.CreateCloudBlobClient();

            var inputContainer = inputClient.GetContainerReference(config["InputContainer"].ToString());
            var outputContainer = outputClient.GetContainerReference(config["OutputContainer"].ToString());

            List<KeyValuePair<string, string>> baseAddresses = new List<KeyValuePair<string, string>>();

            foreach (var pair in config["BaseAddresses"])
            {
                baseAddresses.Add(new KeyValuePair<string, string>(pair["Input"].ToString(), pair["Output"].ToString()));
            }

            // the longest uri has to be first so we get the best match
            baseAddresses = baseAddresses.OrderByDescending(p => p.Key.Length).ToList();

            // list blobs
            ParallelOptions options = new ParallelOptions();
            options.MaxDegreeOfParallelism = threads;

            string inputUri = inputContainer.Uri.AbsoluteUri + "/";
            string[] inputPaths = inputContainer.ListBlobs(null, true).Select(b => Uri.UnescapeDataString(b.Uri.AbsoluteUri.Replace(inputUri, string.Empty))).OrderBy(s => s).ToArray();

            // create container if needed
            if (!outputContainer.Exists())
            {
                outputContainer.Create();

                if (inputContainer.GetPermissions().PublicAccess == BlobContainerPublicAccessType.Container)
                {
                    outputContainer.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Container });
                }
            }

            Console.WriteLine("Copying: " + inputPaths.Length + " blobs");

            // copy blobs
            Parallel.ForEach(inputPaths, options, path =>
            {
                if (_run)
                {
                    var inputBlob = inputContainer.GetBlockBlobReference(path);
                    var outputBlob = outputContainer.GetBlockBlobReference(path);

                    if (_overwrite || !outputBlob.Exists())
                    {
                        int count = 0;

                        var fetchPropsTask = inputBlob.FetchAttributesAsync();

                        using (MemoryStream stream = new MemoryStream())
                        {
                            inputBlob.DownloadToStream(stream);

                            stream.Seek(0, SeekOrigin.Begin);

                            // replace strings found in 
                            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                            {
                                using (StreamReader reader = new StreamReader(stream))
                                {
                                    JObject json = JObject.Parse(reader.ReadToEnd());

                                    foreach (var token in json.Descendants().Select(t => t as JValue).Where(t => t != null && t.Type == JTokenType.String))
                                    {
                                        string val = token.Value.ToString();

                                        foreach (var pair in baseAddresses)
                                        {
                                            // replace the url if we find it
                                            if (val.IndexOf(pair.Key) == 0)
                                            {
                                                count++;
                                                token.Value = pair.Value + val.Substring(pair.Key.Length);
                                                break;
                                            }
                                        }
                                    }

                                    outputBlob.UploadText(json.ToString(), Encoding.UTF8);
                                }
                            }
                            else
                            {
                                outputBlob.UploadFromStream(stream);
                            }
                        }

                        fetchPropsTask.Wait();

                        // update properties
                        outputBlob.Properties.ContentType = inputBlob.Properties.ContentType;
                        outputBlob.Properties.CacheControl = inputBlob.Properties.CacheControl;
                        outputBlob.Properties.ContentEncoding = inputBlob.Properties.ContentEncoding;
                        outputBlob.Properties.ContentDisposition = inputBlob.Properties.ContentDisposition;
                        outputBlob.Properties.ContentLanguage = inputBlob.Properties.ContentLanguage;

                        outputBlob.SetProperties();

                        Console.WriteLine("{0} Changes: {1}", path, count);
                    }
                    else
                    {
                        Console.WriteLine("Already exists: " + path);
                    }
                }
            });

            runtime.Stop();

            Console.WriteLine("Runtime: " + runtime.Elapsed);
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            _run = false;
            Console.WriteLine("Ctrl+C caught. Skipping remaining files.");
        }
    }
}
