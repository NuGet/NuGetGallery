// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Storage
{
    public class FileStorage : Storage
    {
        public FileStorage(string baseAddress, string path, ILogger<FileStorage> logger) 
            : this(new Uri(baseAddress), path, logger) { }

        public FileStorage(Uri baseAddress, string path, ILogger<FileStorage> logger)
            : base(baseAddress, logger)
        {
            Path = path;

            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }

            ResetStatistics();
        }

        //File exists
        public override bool Exists(string fileName)
        {
            return File.Exists(fileName);
        }

        public override Task<bool> ExistsAsync(string fileName, CancellationToken cancellationToken)
        {
            return Task.FromResult(Exists(fileName));
        }

        public override IEnumerable<StorageListItem> List(bool getMetadata)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(Path);
            var files = directoryInfo.GetFiles("*", SearchOption.AllDirectories)
                .Select(file => 
                    new StorageListItem(GetUri(file.FullName.Replace(Path, string.Empty)), file.LastWriteTimeUtc));

            return files.AsEnumerable();
        }

        public override Task<IEnumerable<StorageListItem>> ListAsync(bool getMetadata, CancellationToken cancellationToken)
        {
            // For this storage, there is no difference between Async and sync versions.
            return Task.FromResult<IEnumerable<StorageListItem>>(List(getMetadata));
        }

        public string Path
        { 
            get;
            set;
        }

        protected override Task OnCopyAsync(
            Uri sourceUri,
            IStorage destinationStorage,
            Uri destinationUri,
            IReadOnlyDictionary<string, string> destinationProperties,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        //  save
        protected override async Task OnSave(Uri resourceUri, StorageContent content, bool overwrite, CancellationToken cancellationToken)
        {
            SaveCount++;

            TraceMethod("SAVE", resourceUri);

            string name = GetName(resourceUri);

            string path = Path.Trim('\\') + '\\';

            string[] t = name.Split('/');

            name = t[t.Length - 1];

            if (t.Length > 1)
            {
                for (int i = 0; i < t.Length - 1; i++)
                {
                    string folder = t[i];

                    if (folder != string.Empty)
                    {
                        if (!(new DirectoryInfo(path + folder)).Exists)
                        {
                            DirectoryInfo directoryInfo = new DirectoryInfo(path);
                            directoryInfo.CreateSubdirectory(folder);
                        }

                        path = path + folder + '\\';
                    }
                }
            }

            var fileMode = overwrite ? FileMode.Create : FileMode.CreateNew;

            using (FileStream stream = new FileStream(path + name, fileMode))
            {
                await content.GetContentStream().CopyToAsync(stream,4096, cancellationToken);
            }
        }

        //  load

        protected override async Task<StorageContent> OnLoad(Uri resourceUri, CancellationToken cancellationToken)
        {
            string name = GetName(resourceUri);

            string path = Path.Trim('\\') + '\\';

            string folder = string.Empty;

            string[] t = name.Split('/');
            if (t.Length == 2)
            {
                folder = t[0];
                name = t[1];
            }

            if (folder != string.Empty)
            {
                folder = folder + '\\';
            }

            string filename = path + folder + name;

            FileInfo fileInfo = new FileInfo(filename);
            if (fileInfo.Exists)
            {
                return await Task.Run<StorageContent>(() => { return new StreamStorageContent(fileInfo.OpenRead()); }, cancellationToken);
            }

            return null;
        }

        //  delete

        protected override async Task OnDelete(Uri resourceUri, CancellationToken cancellationToken)
        {
            string name = GetName(resourceUri);

            string path = Path.Trim('\\') + '\\';

            string folder = string.Empty;

            string[] t = name.Split('/');
            if (t.Length == 2)
            {
                folder = t[0];
                name = t[1];
            }

            if (folder != string.Empty)
            {
                folder = folder + '\\';
            }

            string filename = path + folder + name;

            FileInfo fileInfo = new FileInfo(filename);
            if (fileInfo.Exists)
            {
                await Task.Run(() => { fileInfo.Delete(); },cancellationToken);
            }
        }

        public override Task SetMetadataAsync(Uri resourceUri, IDictionary<string, string> metadata)
        {
            throw new NotImplementedException();
        }
    }
}
