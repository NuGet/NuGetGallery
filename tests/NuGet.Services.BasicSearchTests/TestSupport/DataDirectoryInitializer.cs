// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.BasicSearchTests.TestSupport
{
    public class DataDirectoryInitializer
    {
        private readonly TestSettings _settings;

        public DataDirectoryInitializer(TestSettings settings)
        {
            _settings = settings;
        }
        
        public async Task<string> GetInitializedDirectoryAsync()
        {
            Directory.CreateDirectory(_settings.DataDirectory);
            await WriteFileAsync("downloads.v1.json", "[]");
            await WriteFileAsync("curatedfeeds.json", "[]");
            await WriteFileAsync("owners.json", "[]");
            await WriteFileAsync("rankings.v1.json", "{\"Rank\": []}");
            return _settings.DataDirectory;
        }

        private async Task WriteFileAsync(string name, string content)
        {
            string path = Path.Combine(_settings.DataDirectory, name);

            using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(fileStream, new UTF8Encoding(false)))
            {
                await writer.WriteAsync(content);
            }
        }
    }
}