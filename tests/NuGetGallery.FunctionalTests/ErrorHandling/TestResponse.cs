// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.FunctionalTests.ErrorHandling
{
    /// <summary>
    /// A simplified version of <see cref="HttpResponseMessage"/> so we don't have to worry about disposal, streams,
    /// or intepreting some common response bodies.
    /// </summary>
    public class TestResponse
    {
        private TestResponse(
            string relativePath,
            HttpStatusCode statusCode,
            string reasonPhrase,
            string contentTypeHeader,
            string locationHeader,
            int contentLength,
            string content)
        {
            RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
            StatusCode = statusCode;
            ReasonPhrase = reasonPhrase ?? throw new ArgumentNullException(nameof(reasonPhrase));
            ContentTypeHeader = contentTypeHeader;
            LocationHeader = locationHeader;
            ContentLength = contentLength;
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public static async Task<TestResponse> FromHttpResponseMessageAsync(
            string relativePath,
            HttpResponseMessage httpResponseMessage)
        {
            if (httpResponseMessage == null)
            {
                throw new ArgumentNullException(nameof(httpResponseMessage));
            }

            var hasContentType = httpResponseMessage.Content.Headers.TryGetValues("Content-Type", out var contentTypes);
            var hasLocation = httpResponseMessage.Headers.TryGetValues("Location", out var locations);

            var contentBytes = await httpResponseMessage.Content.ReadAsByteArrayAsync();
            var contentString = await httpResponseMessage.Content.ReadAsStringAsync();

            var output = new TestResponse(
                relativePath,
                httpResponseMessage.StatusCode,
                httpResponseMessage.ReasonPhrase,
                hasContentType ? contentTypes.Single() : null,
                hasLocation ? locations.Single() : null,
                contentBytes.Length,
                contentString);

            return output;
        }

        public string RelativePath { get; }
        public HttpStatusCode StatusCode { get; }
        public string ReasonPhrase { get; }
        public string ContentTypeHeader { get; }
        public string LocationHeader { get; }
        public int ContentLength { get; }
        public string Content { get; }

        public bool IsPrettyHtml => Content.Contains("page-error")
            && Content.Contains("Oops")
            && Content.Contains("Get me out of here!");

        public bool IsErrorXml => Content.Contains("<m:message xml:lang=\"en-US\">An error has occurred.</m:message>");

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.Append(nameof(TestResponse));
            sb.AppendLine(":");
            Append(sb, "  StatusCode:        ", (int)StatusCode);
            Append(sb, "  ReasonPhrase:      ", ReasonPhrase);
            Append(sb, "  ContentTypeHeader: ", ContentTypeHeader);
            Append(sb, "  LocationHeader:    ", LocationHeader);
            Append(sb, "  ContentLength:     ", ContentLength);
            Append(sb, "  IsPrettyHtml:      ", IsPrettyHtml);
            Append(sb, "  IsErrorXml:        ", IsErrorXml);

            return sb.ToString();
        }

        private void Append(StringBuilder sb, string label, int value) => Append(sb, label, value, quotes: false);
        private void Append(StringBuilder sb, string label, bool value) => Append(sb, label, value, quotes: false);
        private void Append(StringBuilder sb, string label, string value) => Append(sb, label, value, quotes: true);

        private void Append(StringBuilder sb, string label, object value, bool quotes)
        {
            sb.Append(label);

            if (value == null)
            {
                sb.Append("(none)");
            }
            else
            {
                if (quotes)
                {
                    sb.AppendFormat("'{0}'", value);
                }
                else
                {
                    sb.Append(value);
                }
            }

            sb.AppendLine();
        }
    }
}