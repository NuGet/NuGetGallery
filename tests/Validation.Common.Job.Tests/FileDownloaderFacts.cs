// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation;
using Xunit;
using Xunit.Abstractions;

namespace Validation.Common.Job.Tests
{
    public class FileDownloaderFacts
    {
        public class TheDownloadAsyncMethodWithExpectedFileSize : Facts
        {
            public TheDownloadAsyncMethodWithExpectedFileSize(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task AcceptsFileThatIsTheCorrectSize()
            {
                using (var result = await Target.DownloadExpectedFileSizeAsync(Url, 128, Token))
                using (var buffer = new MemoryStream())
                {
                    await result.Stream.CopyToAsync(buffer);

                    Assert.Equal(Bytes, buffer.ToArray());
                }
            }

            [Fact]
            public async Task RejectsFileThatIsTooLarge()
            {
                using (var result = await Target.DownloadExpectedFileSizeAsync(Url, 127, Token))
                {
                    Assert.Equal(FileDownloadResultType.UnexpectedFileSize, result.Type);
                }
            }

            [Fact]
            public async Task RejectsFileThatIsTooSmall()
            {
                using (var result = await Target.DownloadExpectedFileSizeAsync(Url, 129, Token))
                {
                    Assert.Equal(FileDownloadResultType.UnexpectedFileSize, result.Type);
                }
            }

            [Fact]
            public async Task RejectsFileThatIsNotFound()
            {
                UrlToResponseStream.Clear();
                using (var result = await Target.DownloadExpectedFileSizeAsync(Url, 128, Token))
                {
                    Assert.Equal(FileDownloadResultType.NotFound, result.Type);
                }
            }
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                UrlToResponseStream = new Dictionary<Uri, Stream>();
                HttpClient = new HttpClient(new TestHandler(UrlToResponseStream));
                TelemetryService = new Mock<ICommonTelemetryService>();
                Options = new Mock<IOptionsSnapshot<FileDownloaderConfiguration>>();
                Config = new FileDownloaderConfiguration();
                Options.Setup(x => x.Value).Returns(() => Config);
                Logger = new LoggerFactory()
                    .AddXunit(output)
                    .CreateLogger<FileDownloader>();

                Url = new Uri("https://example/nuget.versioning.nupkg");
                Bytes = Enumerable.Range(0, 128).Select(i => (byte)i).ToArray();
                UrlToResponseStream[Url] = new MemoryStream(Bytes);
                Token = CancellationToken.None;

                Target = new FileDownloader(
                    HttpClient,
                    TelemetryService.Object,
                    Options.Object,
                    Logger);
            }

            public Dictionary<Uri, Stream> UrlToResponseStream { get; }
            public HttpClient HttpClient { get; }
            public Mock<ICommonTelemetryService> TelemetryService { get; }
            public Mock<IOptionsSnapshot<FileDownloaderConfiguration>> Options { get; }
            public FileDownloaderConfiguration Config { get; }
            public ILogger<FileDownloader> Logger { get; }
            public Uri Url { get; }
            public byte[] Bytes { get; }
            public CancellationToken Token { get; }
            public FileDownloader Target { get; }
        }

        public class TestHandler : HttpMessageHandler
        {
            private readonly IReadOnlyDictionary<Uri, Stream> _urlToResponseStream;

            public TestHandler(IReadOnlyDictionary<Uri, Stream> urlToResponseStream)
            {
                _urlToResponseStream = urlToResponseStream;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(Send(request));
            }

            private HttpResponseMessage Send(HttpRequestMessage request)
            {
                if (request.Method != HttpMethod.Get
                    || !_urlToResponseStream.TryGetValue(request.RequestUri, out var responseStream))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    RequestMessage = request,
                    Content = new StreamContent(responseStream),
                };
            }
        }
    }
}
