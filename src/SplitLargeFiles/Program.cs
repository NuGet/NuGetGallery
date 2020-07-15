// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.GZip;
using Microsoft.WindowsAzure.Storage;
using NuGet.Services.KeyVault;

namespace NuGet.Tools.SplitLargeFiles
{
    public class Program
    {
        private const string GzipExtension = ".gz";

        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0].Equals("upload", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Executing the 'upload' command.");
                Console.WriteLine();
                return ExecuteUploadAsync(args).GetAwaiter().GetResult();
            }
            else
            {
                Console.WriteLine("Executing the 'split' command. Use 'upload' as the first argument to run the upload command.");
                Console.WriteLine();
                return ExecuteSplit(args);
            }
        }

        private static async Task<int> ExecuteUploadAsync(string[] args)
        {
            if (args.Length < 5)
            {
                Console.WriteLine("The first parameter is the command name: 'upload'.");
                Console.WriteLine("The second parameter must be a directory to non-recursively enumerate for files to upload.");
                Console.WriteLine("The third parameter must be a wildcard pattern of file names to upload.");
                Console.WriteLine("The fourth parameter must be a Azure Storage connection string.");
                Console.WriteLine("The fifth parameter must be a blob storage container to upload the files to.");
                Console.WriteLine("The optional sixth parameter can be a KeyVault name to inject secrets from. A managed identity will be used.");
                Console.WriteLine();
                Console.WriteLine("After uploading each file to the root of the storage container, the file will be deleted.");
                Console.WriteLine("The blob name will be the same as the original file name.");
                return 1;
            }

            var directory = args[1];
            var pattern = args[2];
            var connectionString = args[3];
            var containerName = args[4];

            if (args.Length >= 6)
            {
                var vaultName = args[5];
                Console.WriteLine($"Injecting secrets into the connection string from KeyVault '{vaultName}'...");
                var keyVaultReader = new KeyVaultReader(new KeyVaultConfiguration(vaultName));
                var secretInjector = new SecretInjector(keyVaultReader);
                connectionString = await secretInjector.InjectAsync(connectionString);
            }

            Console.Write($"Searching for '{pattern}' in {directory}...");
            var files = Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly).ToList();
            Console.WriteLine($" found {files.Count} files to upload.");

            var account = CloudStorageAccount.Parse(connectionString);
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(containerName);

            Console.Write($"Checking the connection to {account.BlobEndpoint.AbsoluteUri}, container '{containerName}'...");
            var exists = await container.ExistsAsync();
            if (exists)
            {
                Console.WriteLine(" the container exists.");
            }
            else
            {
                Console.WriteLine(" the container does not exist. Halting.");
                return 1;
            }

            Console.WriteLine();

            var failures = 0;
            foreach (var filePath in files)
            {
                try
                {
                    var fileName = Path.GetFileName(filePath);
                    var blob = container.GetBlockBlobReference(fileName);
                    Console.WriteLine($"File src: {filePath}");
                    Console.WriteLine($"Blob dst: {blob.Uri.AbsoluteUri}");
                    Console.Write("  Uploading the file to blob storage...");
                    await blob.UploadFromFileAsync(filePath);
                    Console.WriteLine(" done.");
                    Console.Write("  Deleting the file from the file system...");
                    File.Delete(filePath);
                    Console.WriteLine(" done.");
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    failures++;
                    Console.WriteLine(" failed. Continuing to the next file.");
                    Console.WriteLine(ex.ToString());
                    Console.WriteLine();
                }
            }

            return failures;
        }

        private static int ExecuteSplit(string[] args)
        {
            var desiredFileSize = 20 * 1024 * 1024;
            var fileIndexFormat = "_{0:D4}";
            if (args.Length == 0)
            {
                Console.WriteLine("The first parameter should be the path to the large file to split.");
                Console.WriteLine($"The optional second parameter should be the desired file size in bytes. Default is {desiredFileSize} bytes.");
                Console.WriteLine($"The optional third parameter is the file index suffix. Default is '{fileIndexFormat}'.");
                return 1;
            }

            var inputPath = args[0];

            if (args.Length > 1 && !int.TryParse(args[1], out desiredFileSize))
            {
                Console.WriteLine($"Unable to parse desired file size: {args[1]}");
                return 1;
            }

            if (desiredFileSize < 1)
            {
                Console.WriteLine("The desired file size must be larger than zero.");
                return 1;
            }

            if (args.Length > 2)
            {
                fileIndexFormat = args[2];
            }

            Console.WriteLine("Input parameters:");
            Console.WriteLine();
            Console.WriteLine($"  Input path: {inputPath}");
            Console.WriteLine($"  Desired output file size (bytes): {desiredFileSize}");
            Console.WriteLine($"  File Index Format: {fileIndexFormat} (ex: '{string.Format(fileIndexFormat, 0)}')");
            Console.WriteLine();

            if (!File.Exists(inputPath))
            {
                Console.WriteLine("The input file does not exist.");
                return 1;
            }

            using (var inputStream = new FileStream(inputPath, FileMode.Open))
            {

                Console.WriteLine("Gathering data about the input file...");
                var properties = FileProperties.FromFileStream(inputStream);

                // Generate the output path format.
                var outputPathFormat = GenerateOutputPathFormat(inputPath, fileIndexFormat, properties.IsGzipped);

                // This is a rough estimate assuming that each line compresses to the same length.
                var fileCount = GetFileCount(properties.Size, desiredFileSize);
                var linesPerFile = properties.LineCount / fileCount;

                Console.WriteLine();
                Console.WriteLine($"  Output path format: {outputPathFormat}");
                Console.WriteLine();
                Console.WriteLine($"  Input File encoding: {properties.Encoding}");
                Console.WriteLine($"  Is gzipped: {properties.IsGzipped}");
                Console.WriteLine($"  Input file size (bytes): {properties.Size}");
                Console.WriteLine($"  Input file line count: {properties.LineCount}");
                Console.WriteLine();
                Console.WriteLine($"  Output file count: {fileCount}");
                Console.WriteLine($"  Output file line count: {linesPerFile}");
                Console.WriteLine();

                Console.WriteLine("Splitting up files...");

                Stream readStream = inputStream;
                if (properties.IsGzipped)
                {
                    readStream = new GZipInputStream(inputStream);
                }

                using (var reader = new StreamReader(readStream, properties.Encoding))
                {
                    for (var fileIndex = 0; fileIndex < fileCount; fileIndex++)
                    {
                        var isLastFile = fileIndex >= fileCount - 1;
                        var outputPath = string.Format(outputPathFormat, fileIndex);

                        Console.Write($"[{fileIndex + 1}/{fileCount}] Writing {outputPath}...");
                        WriteOutputFile(properties, reader, linesPerFile, outputPath, isLastFile);
                        Console.WriteLine(" done.");
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("The file has been split up.");

            return 0;
        }

        public static long GetFileCount(long fileSize, long desiredFileSize)
        {
            if (fileSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fileSize));
            }

            if (desiredFileSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(desiredFileSize));
            }

            if (fileSize < desiredFileSize)
            {
                return 1;
            }

            var fileCount = fileSize / desiredFileSize;
            if (fileSize % desiredFileSize > 0)
            {
                fileCount++;
            }

            return fileCount;
        }

        private static string GenerateOutputPathFormat(string inputPath, string fileIndexFormat, bool isGzipped)
        {
            var directory = Path.GetDirectoryName(inputPath);
            var fileName = Path.GetFileName(inputPath);

            var gzipSuffix = string.Empty;
            if (isGzipped && inputPath.EndsWith(GzipExtension, StringComparison.OrdinalIgnoreCase))
            {
                gzipSuffix = Path.GetExtension(fileName);
                fileName = Path.GetFileNameWithoutExtension(fileName);
            }

            var withoutExtension = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);

            return Path.Combine(directory, withoutExtension + fileIndexFormat + extension + gzipSuffix);
        }

        private static void WriteOutputFile(FileProperties properties, StreamReader reader, long linesPerFile, string outputPath, bool isLastFile)
        {
            using (var outputStream = new FileStream(outputPath, FileMode.Create))
            {
                Stream writeStream = outputStream;
                if (properties.IsGzipped)
                {
                    writeStream = new GZipOutputStream(writeStream);
                }

                var lineIndex = 0;
                using (var writer = new StreamWriter(writeStream, properties.Encoding))
                {
                    string line;
                    while (((!isLastFile && lineIndex < linesPerFile) || isLastFile) &&
                           (line = reader.ReadLine()) != null)
                    {
                        lineIndex++;
                        writer.WriteLine(line);
                    }
                }
            }
        }
    }
}
