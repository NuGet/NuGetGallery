using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Search.Client;
using Xunit;

namespace NuGetGallery.SearchClient
{
    public class RequestInspectingHandler 
        : DelegatingHandler
    {
        public List<HttpRequestMessage> Requests { get; private set; }

        public RequestInspectingHandler()
        {
            Requests = new List<HttpRequestMessage>();
            InnerHandler = new HttpClientHandler();
        }
    
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            return base.SendAsync(request, cancellationToken);
        }
    }

    public class RetryingHttpClientWrapperFacts
    {
        private static readonly Uri ValidUri1 = new Uri("http://www.microsoft.com");
        private static readonly Uri ValidUri2 = new Uri("http://www.bing.com");
        private static readonly Uri InvalidUri1 = new Uri("http://nonexisting.domain.atleast.ihope");
        private static readonly Uri InvalidUri2 = new Uri("http://nonexisting.domain.atleast.ihope/foo");
        private static readonly Uri InvalidUri3 = new Uri("http://www.nuget.org/packages?q=%22Windows&sortOrder=package-download-count&page=7&prerelease=False");
        private static readonly Uri InvalidUriWith404 = new Uri("http://www.nuget.org/thisshouldreturna404page");

        private RetryingHttpClientWrapper CreateWrapperClient(HttpMessageHandler handler)
        {
            return new RetryingHttpClientWrapper(new HttpClient(handler));
        }

        private RetryingHttpClientWrapper CreateWrapperClient()
        {
            return new RetryingHttpClientWrapper(new HttpClient());
        }

        [Fact]
        public void ReturnsStringForValidUri()
        {
            var client = CreateWrapperClient();

            var result = client.GetStringAsync(new[] { ValidUri1 }).Result;

            Assert.NotNull(result);
        }

        [Fact]
        public void ReturnsSuccessResponseForValidUri()
        {
            var client = CreateWrapperClient();

            var result = client.GetAsync(new[] { ValidUri1 }).Result;

            Assert.True(result.IsSuccessStatusCode);
        }

        [Fact]
        public void ReturnsStringForCollectionContainingValidUri()
        {
            var inspectingHandler = new RequestInspectingHandler();
            var client = CreateWrapperClient(inspectingHandler);

            var result = client.GetStringAsync(new[] { InvalidUri1, InvalidUri2, ValidUri1, InvalidUri3 }).Result;

            Assert.NotNull(result);
        }

        [Fact]
        public void ReturnsSuccessResponseForCollectionContainingValidUri()
        {
            var inspectingHandler = new RequestInspectingHandler();
            var client = CreateWrapperClient(inspectingHandler);

            var result = client.GetAsync(new[] { InvalidUri1, InvalidUri2, ValidUri1, InvalidUri3 }).Result;

            Assert.True(result.IsSuccessStatusCode);
            Assert.Equal(ValidUri1, result.RequestMessage.RequestUri);
        }

        [Fact]
        public void LoadBalancesBetweenValidUrisForGetStringAsync()
        {
            var inspectingHandler = new RequestInspectingHandler();
            var client = CreateWrapperClient(inspectingHandler);

            bool hasHitUri1 = false;
            bool hasHitUri2 = false;

            int numRequests = 0;
            while (!hasHitUri1 || !hasHitUri2 || numRequests < 25)
            {
                numRequests++;
                var result = client.GetStringAsync(new[] { ValidUri1, ValidUri2 }).Result;

                Assert.NotNull(result);
                if (!hasHitUri1) hasHitUri1 = inspectingHandler.Requests.Any(r => r.RequestUri == ValidUri1);
                if (!hasHitUri2) hasHitUri2 = inspectingHandler.Requests.Any(r => r.RequestUri == ValidUri2);
            }

            Assert.True(hasHitUri1, "The first valid Uri has not been hit within the limit of " + numRequests + " requests.");
            Assert.True(hasHitUri2, "The second valid Uri has not been hit within the limit of " + numRequests + " requests.");
        }

        [Fact]
        public void LoadBalancesBetweenValidUrisForGetAsync()
        {
            var inspectingHandler = new RequestInspectingHandler();
            var client = CreateWrapperClient(inspectingHandler);

            bool hasHitUri1 = false;
            bool hasHitUri2 = false;

            int numRequests = 0;
            while (!hasHitUri1 || !hasHitUri2 || numRequests < 25)
            {
                numRequests++;
                var result = client.GetAsync(new[] { ValidUri1, ValidUri2 }).Result;

                Assert.NotNull(result);
                if (!hasHitUri1) hasHitUri1 = inspectingHandler.Requests.Any(r => r.RequestUri == ValidUri1);
                if (!hasHitUri2) hasHitUri2 = inspectingHandler.Requests.Any(r => r.RequestUri == ValidUri2);
            }

            Assert.True(hasHitUri1, "The first valid Uri has not been hit within the limit of " + numRequests + " requests.");
            Assert.True(hasHitUri2, "The second valid Uri has not been hit within the limit of " + numRequests + " requests.");
        }

        [Fact]
        public void FailsWhenNoValidUriGiven1()
        {
            var client = CreateWrapperClient();

            Assert.Throws<AggregateException>(() => client.GetStringAsync(new[] { InvalidUri1, InvalidUri2 }).Result);
        }

        [Fact]
        public void FailsWhenNoValidUriGiven2()
        {
            var client = CreateWrapperClient();

            Assert.Throws<AggregateException>(() => client.GetAsync(new[] { InvalidUri1, InvalidUri2 }).Result);
        }

        [Fact]
        public void Returns404When404IsExpected()
        {
            var client = CreateWrapperClient();

            var result = client.GetAsync(new[] { InvalidUriWith404, InvalidUri3 }).Result;

            Assert.False(result.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        }
    }
}
