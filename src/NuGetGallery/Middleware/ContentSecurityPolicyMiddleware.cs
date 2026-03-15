// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace NuGetGallery.Middleware
{
    /// <summary>
    /// ASP.NET Core middleware for Content Security Policy headers
    /// Migrated from OWIN CSP middleware in OwinStartup.cs
    /// </summary>
    public class ContentSecurityPolicyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;

        public ContentSecurityPolicyMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var fontAndIconSrc = string.Concat("https://res-1.cdn.office.net/files/fabric-cdn-prod_20221201.001/assets/fonts/", ' ',
                     "https://res-1.cdn.office.net/files/fabric-cdn-prod_20221201.001/assets/icons/");

            var scriptFileHashes =
            string.Concat(
              "'sha512-gU7kztaQEl7SHJyraPfZLQCNnrKdaQi5ndOyt4L4UPL/FHDd/uB9Je6KDARIqwnNNE27hnqoWLBq+Kpe4iHfeQ=='", ' ',
              "'sha512-DXYctkkhmMYJ4vYp4Dm6jprD4ZareZ7ud/d9mGCKif/Dt3FnN95SjogHvwKvxXHoMAAkZX6EO6ePwpDIR1Y8jw=='", ' ',
              "'sha512-mz4SrGyk+dtPY9MNYOMkD81gp8ajViZ4S0VDuM/Zqg40cg9xgIBYSiL5fN79Htbz4f2+uR9lrDO6mgcjM+NAXA=='", ' ',
              "'sha512-pnt8OPBTOklRd4/iSW7msOiCVO4uvffF17Egr3c7AaN0h3qFnSu7L6UmdZJUCednMhhruTLRq7X9WbyAWNBegw=='", ' '
             );

            using var rng = RandomNumberGenerator.Create();
            var nonceBytes = new byte[32];
            rng.GetBytes(nonceBytes);
            var nonce = Convert.ToBase64String(nonceBytes);
            var reportUri = _configuration["Gallery:CspReportUri"];

            var contentSecurityPolicyReportHeader = string.Format(
                "default-src 'self' 'nonce-{0}' 'strict-dynamic' https:; script-src 'nonce-{0}' {3} 'strict-dynamic' https:; font-src 'self' {1} 'nonce-{0}'; base-uri 'none'; form-action 'self' 'nonce-{0}'; style-src 'self' 'nonce-{0}'; report-uri {2}; object-src 'none'; frame-ancestors 'none'; ",
                nonce, fontAndIconSrc, reportUri, scriptFileHashes);

            context.Items["cspNonce"] = nonce;
            context.Response.Headers.Add("Content-Security-Policy-Report-Only", contentSecurityPolicyReportHeader);
            context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");

            await _next(context);
        }
    }
}
