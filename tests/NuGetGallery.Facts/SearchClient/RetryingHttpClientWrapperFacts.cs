// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Services.Search.Client;
using Xunit;

namespace NuGetGallery.SearchClient
{
    public class RetryingHttpClientWrapperFacts
    {
        private static readonly Uri ValidUri1 = new Uri("https://www.microsoft.com/en-us/");
        private static readonly Uri ValidUri2 = new Uri("https://www.nuget.org");
        private static readonly Uri InvalidUri1 = new Uri("http://nonexisting.domain.atleast.ihope");
        private static readonly Uri InvalidUri2 = new Uri("http://nonexisting.domain.atleast.ihope/foo");
        private static readonly Uri InvalidUri3 = new Uri("http://www.nuget.org/com/ibm/mq/com.ibm.mq.soap/7.0.1.10/com.ibm.mq.soap-7.0.1.10");
        private static readonly Uri InvalidUriWith404 = new Uri("http://www.nuget.org/thisshouldreturna404page");

        private RetryingHttpClientWrapper CreateWrapperClient(HttpMessageHandler handler)
        {
            return new RetryingHttpClientWrapper(new HttpClient(handler), (exception) => { });
        }

        private RetryingHttpClientWrapper CreateWrapperClient()
        {
            return new RetryingHttpClientWrapper(new HttpClient(), (exception) => { });
        }

        [Fact]
        public async Task ReturnsStringForValidUri()
        {
            var client = CreateWrapperClient();

            var result = await client.GetStringAsync(new[] { ValidUri1 });

            Assert.NotNull(result);
        }

        [Fact]
        public async Task ReturnsSuccessResponseForValidUri()
        {
            var client = CreateWrapperClient();

            var result = await client.GetAsync(new[] { ValidUri1 });

            Assert.True(result.IsSuccessStatusCode);
        }

        [Fact]
        public async Task ReturnsStringForCollectionContainingValidUri()
        {
            var inspectingHandler = new RequestInspectingHandler();
            var client = CreateWrapperClient(inspectingHandler);

            var result = await client.GetStringAsync(new[] { InvalidUri1, InvalidUri2, ValidUri1, InvalidUri3 });

            Assert.NotNull(result);
        }

        [Fact]
        public async Task ReturnsSuccessResponseForCollectionContainingValidUri()
        {
            var inspectingHandler = new RequestInspectingHandler();
            var client = CreateWrapperClient(inspectingHandler);

            var result = await client.GetAsync(new[] { InvalidUri1, InvalidUri2, ValidUri1, InvalidUri3 });

            Assert.True(result.IsSuccessStatusCode);
            Assert.Equal(ValidUri1, result.RequestMessage.RequestUri);
        }

        [Fact]
        public async Task LoadBalancesBetweenValidUrisForGetStringAsync()
        {
            var inspectingHandler = new RequestInspectingHandler();
            var client = CreateWrapperClient(inspectingHandler);

            bool hasHitUri1 = false;
            bool hasHitUri2 = false;

            int numRequests = 0;
            while ((!hasHitUri1 || !hasHitUri2) && numRequests < 25)
            {
                numRequests++;
                var result = await client.GetStringAsync(new[] { ValidUri1, ValidUri2 });

                Assert.NotNull(result);
                if (!hasHitUri1) hasHitUri1 = inspectingHandler.Requests.Any(r => r.RequestUri == ValidUri1);
                if (!hasHitUri2) hasHitUri2 = inspectingHandler.Requests.Any(r => r.RequestUri == ValidUri2);
            }

            Assert.True(hasHitUri1, "The first valid Uri has not been hit within the limit of " + numRequests + " requests.");
            Assert.True(hasHitUri2, "The second valid Uri has not been hit within the limit of " + numRequests + " requests.");
        }

        [Fact]
        public async Task LoadBalancesBetweenValidUrisForGetAsync()
        {
            var inspectingHandler = new RequestInspectingHandler();
            var client = CreateWrapperClient(inspectingHandler);

            int hitUri1Count = 0;
            int hitUri2Count = 0;
            var result = await client.GetAsync(new[] { ValidUri1, ValidUri2 });

            Assert.NotNull(result);
            if (inspectingHandler.Requests.Any(r => r.RequestUri == ValidUri1))
            {
                hitUri1Count++;
            }
            if (inspectingHandler.Requests.Any(r => r.RequestUri == ValidUri2))
            {
                hitUri2Count++;
            }
            // Because the algorithm for choosing uri1 or uri2 is random asserting that the one or the other uri is not reliable
            // However the sum of requests between two uri requests needs to be larger or equal with 1
            Assert.True(hitUri1Count + hitUri2Count >= 1, $"The uri1 had {hitUri1Count} hits and the uri2 had {hitUri2Count} hits. The sum will need to be at least 1.");
        }

        [Fact]
        public async Task FailsWhenNoValidUriGiven1()
        {
            var client = CreateWrapperClient();

            await Assert.ThrowsAsync<AggregateException>(() => client.GetStringAsync(new[] { InvalidUri1, InvalidUri2 }));
        }

        [Fact]
        public async Task FailsWhenNoValidUriGiven2()
        {
            var client = CreateWrapperClient();

            await Assert.ThrowsAsync<AggregateException>(() => client.GetAsync(new[] { InvalidUri1, InvalidUri2 }));
        }

        [Fact]
        public async Task Returns404When404IsExpected()
        {
            var client = CreateWrapperClient();

            var result = await client.GetAsync(new[] { InvalidUriWith404, InvalidUri3 });

            Assert.False(result.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        }

        [Theory]
        [InlineData("https://dummy1/", 0, 62)]
        [InlineData("https://dummy1/", 100, 62)]
        [InlineData("https://dummy1/", 62, 62)]
        [InlineData("https://dummy1/", 0, 0)]
        public void WeightedRandomComparerTest(string dummyUrl, int dummyUrlHealthIndex, int otherHealthIndex)
        {
            var comparer = new RetryingHttpClientWrapper.WeightedRandomComparer();

            List<Uri> urls = new List<Uri>()
            {
                new Uri(dummyUrl),
                new Uri ("https://dumm2/")
            };

            //The Assert is very loose verification 
            //In the presence of the bug the line below will be infinite loop
            var orderList = urls.OrderByDescending((u) =>
            {
                if (u.AbsoluteUri == dummyUrl)
                {
                    return dummyUrlHealthIndex;
                }
                else
                {
                    return otherHealthIndex;
                }
            }, comparer).ToList();

            Assert.Contains(dummyUrl, orderList.Select(u => u.AbsoluteUri));
        }
    }
}
