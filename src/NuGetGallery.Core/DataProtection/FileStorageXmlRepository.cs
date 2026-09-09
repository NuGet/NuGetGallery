// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Logging;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.DataProtection
{
    public class FileStorageXmlRepository : IXmlRepository
    {
        private const int ReadAttemptCount = 5;
        private static readonly TimeSpan ReadRetryDelay = TimeSpan.FromMilliseconds(20);

        private readonly ICoreFileStorageService _storage;
        private readonly IDiagnosticsSource _trace;

        public FileStorageXmlRepository(ICoreFileStorageService storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _trace = NullDiagnosticsSource.Instance;
        }

        public FileStorageXmlRepository(
            ICoreFileStorageService storage,
            IDiagnosticsService diagnosticsService)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _trace = diagnosticsService?.SafeGetSource(nameof(FileStorageXmlRepository))
                ?? throw new ArgumentNullException(nameof(diagnosticsService));
        }

        public IReadOnlyCollection<XElement> GetAllElements()
        {
            return RunSynchronously(GetAllElementsAsync);
        }

        public void StoreElement(XElement element, string friendlyName)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            string fileName = GetFileName(friendlyName);
            RunSynchronously(() => StoreElementAsync(element, fileName));
        }

        private async Task<IReadOnlyCollection<XElement>> GetAllElementsAsync()
        {
            IReadOnlyList<string> files = await _storage
                .ListFilesAsync(CoreConstants.Folders.DataProtectionFolderName)
                .ConfigureAwait(false);
            var elements = new List<XElement>();

            foreach (string fileName in files
                .Where(name => name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name, StringComparer.Ordinal))
            {
                using (Stream stream = await GetFileWithRetryAsync(fileName).ConfigureAwait(false))
                {
                    if (stream == null)
                    {
                        throw new InvalidOperationException("A listed Data Protection key-ring file could not be read.");
                    }

                    try
                    {
                        elements.Add(ReadElement(stream));
                    }
                    catch (XmlException exception)
                    {
                        throw new InvalidOperationException("A Data Protection key-ring file contains malformed XML.", exception);
                    }
                }
            }

            _trace.TraceEvent(
                LogLevel.Information,
                eventId: 0,
                message: $"Loaded {elements.Count} Data Protection key-ring entries.");
            return elements;
        }

        private async Task<Stream> GetFileWithRetryAsync(string fileName)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return await _storage
                        .GetFileAsync(CoreConstants.Folders.DataProtectionFolderName, fileName)
                        .ConfigureAwait(false);
                }
                catch (IOException) when (attempt < ReadAttemptCount)
                {
                    await Task.Delay(ReadRetryDelay).ConfigureAwait(false);
                }
            }
        }

        private async Task StoreElementAsync(XElement element, string fileName)
        {
            using (var stream = new MemoryStream())
            {
                var settings = new XmlWriterSettings
                {
                    CloseOutput = false,
                    Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    Indent = false,
                    OmitXmlDeclaration = false,
                };

                using (XmlWriter writer = XmlWriter.Create(stream, settings))
                {
                    element.Save(writer);
                }

                stream.Position = 0;
                await _storage.SaveFileAsync(
                    CoreConstants.Folders.DataProtectionFolderName,
                    fileName,
                    CoreConstants.XmlContentType,
                    stream,
                    overwrite: false).ConfigureAwait(false);
            }

            _trace.TraceEvent(
                LogLevel.Information,
                eventId: 0,
                message: "Stored a new immutable Data Protection key-ring entry.");
        }

        private static XElement ReadElement(Stream stream)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            };

            using (XmlReader reader = XmlReader.Create(stream, settings))
            {
                return XElement.Load(reader, LoadOptions.PreserveWhitespace);
            }
        }

        private static string GetFileName(string friendlyName)
        {
            if (string.IsNullOrWhiteSpace(friendlyName))
            {
                friendlyName = $"key-{Guid.NewGuid():N}";
            }

            if (!string.Equals(Path.GetFileName(friendlyName), friendlyName, StringComparison.Ordinal)
                || friendlyName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("The Data Protection key friendly name is invalid.", nameof(friendlyName));
            }

            return friendlyName + ".xml";
        }

        private static T RunSynchronously<T>(Func<Task<T>> operation)
        {
            return Task.Run(operation).GetAwaiter().GetResult();
        }

        private static void RunSynchronously(Func<Task> operation)
        {
            Task.Run(operation).GetAwaiter().GetResult();
        }
    }
}
