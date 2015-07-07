// // Copyright (c) .NET Foundation. All rights reserved.
// // Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using FluentLinkChecker;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.WebPages
{
    public class FluentLinkChecker
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public FluentLinkChecker(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        public bool TestLinksOnWebPage(string url)
        {
            _testOutputHelper.WriteLine("Testing for broken links on web page '{0}'...", url);

            var uri = new Uri(url);
            var result = LinkCheck
                .On(src => src.Url(uri).Relative())
                .AsBot(bot => bot.Bing())
                .Start();

            foreach (var link in result)
            {
                _testOutputHelper.WriteLine(" Tested link to URL '{0}': Status Code = {1}", link.Url, link.StatusCode);
                if (link.StatusCode != HttpStatusCode.OK)
                {
                    return false;
                }
            }

            // All status codes returned are OK
            return true;
        }
    }
}