// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class FileStorage : Storage
    {
        public FileStorage(string baseAddress, string path, bool verbose)
            : this(new Uri(baseAddress), path, verbose)
        {
        }

        public FileStorage(Uri baseAddress, string path, bool verbose)
            : base(baseAddress)
        {
            Path = path;

            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }

            Verbose = verbose;
        }

        //File exists
        public override bool Exists(string fileName)
        {
            return File.Exists(fileName);
        }

        public override Task<IEnumerable<StorageListItem>> ListAsync(CancellationToken cancellationToken)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(Path);
            var files = directoryInfo.GetFiles("*", SearchOption.AllDirectories)
                .Select(file =>
                    new StorageListItem(GetUri(file.FullName.Replace(Path, string.Empty)), file.LastWriteTimeUtc));

            return Task.FromResult(files.AsEnumerable());
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

        protected override async Task OnSaveAsync(Uri resourceUri, StorageContent content, CancellationToken cancellationToken)
        {
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

            using (FileStream stream = File.Create(path + name))
            {
                await content.GetContentStream().CopyToAsync(stream, 4096, cancellationToken);
            }
        }

        protected override async Task<StorageContent> OnLoadAsync(Uri resourceUri, CancellationToken cancellationToken)
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

        protected override async Task OnDeleteAsync(Uri resourceUri, DeleteRequestOptions deleteRequestOptions, CancellationToken cancellationToken)
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
                await Task.Run(() => { fileInfo.Delete(); }, cancellationToken);
            }
        }
    }
}