// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Search.Client
{
    public sealed class RetryingHttpClientWrapper
    {
        private readonly HttpClient _httpClient;
        private readonly IEndpointHealthIndicatorStore _endpointHealthIndicatorStore;

        private static readonly Random Random = new Random((int) DateTime.UtcNow.Ticks);
        private static readonly int PeriodToDelayAlternateRequest = 3000;
        private static readonly IComparer<int> HealthComparer;

        static RetryingHttpClientWrapper()
        {
            HealthComparer = new WeightedRandomComparer(Random);
        }

        public RetryingHttpClientWrapper(HttpClient httpClient)
            : this (httpClient, new BaseUrlHealthIndicatorStore(new NullHealthIndicatorLogger()))
        {
            _httpClient = httpClient;
        }

        public RetryingHttpClientWrapper(HttpClient httpClient, IEndpointHealthIndicatorStore endpointHealthIndicatorStore)
        {
            _httpClient = httpClient;
            _endpointHealthIndicatorStore = endpointHealthIndicatorStore;
        }

        public async Task<string> GetStringAsync(IEnumerable<Uri> endpoints)
        {
            return await GetWithRetry(endpoints, (client, uri, cancellationToken) => _httpClient.GetStringAsync(uri));
        }

        public async Task<HttpResponseMessage> GetAsync(IEnumerable<Uri> endpoints)
        {
            return await GetWithRetry(endpoints, (client, uri, cancellationToken) => _httpClient.GetAsync(uri, cancellationToken));
        }
        
        private async Task<TResponseType> GetWithRetry<TResponseType>(IEnumerable<Uri> endpoints, Func<HttpClient, Uri, CancellationToken, Task<TResponseType>> run)
        {
            // Build endpoints, ordered by health (with a chance of less health)
            var healthyEndpoints = endpoints.OrderByDescending(e => _endpointHealthIndicatorStore.GetHealth(e), HealthComparer).ToList();

            // Make all requests cancellable using this CancellationTokenSource
            var cancellationTokenSource = new CancellationTokenSource();

            // Create requests queue
            var tasks = CreateRequestQueue(healthyEndpoints, run, cancellationTokenSource);

            // When the first succesful task comes in, return it. If no succesfull tasks are returned, throw an AggregateException.
            var exceptions = new List<Exception>();

            var taskList = tasks.ToList();
            Task<TResponseType> completedTask;
            do
            {
                completedTask = await Task.WhenAny(taskList);
                taskList.Remove(completedTask);

                if (completedTask.Exception != null)
                {
                    exceptions.AddRange(completedTask.Exception.InnerExceptions);
                }
            } while ((completedTask.IsFaulted || completedTask.IsCanceled) && taskList.Any());

            cancellationTokenSource.Cancel(false);

            if (completedTask == null || completedTask.IsFaulted || completedTask.IsCanceled)
            {
                var exceptionsToThrow = exceptions.Where(e => !(e is TaskCanceledException)).ToList();
                if (exceptionsToThrow.Count == 1)
                {
                    throw exceptionsToThrow.First();
                }
                throw new AggregateException(exceptions);
            }
            return await completedTask;
        }

        private List<Task<TResponseType>> CreateRequestQueue<TResponseType>(List<Uri> endpoints, Func<HttpClient, Uri, CancellationToken, Task<TResponseType>> run, CancellationTokenSource cancellatonTokenSource)
        {
            // Queue up a series of requests. Make each request wait a little longer.
            var tasks = new List<Task<TResponseType>>(endpoints.Count);

            for (var i = 0; i < endpoints.Count; i++)
            {
                var endpoint = endpoints[i];

                tasks.Add(Task.Delay(i * PeriodToDelayAlternateRequest, cancellatonTokenSource.Token)
                    .ContinueWith(task =>
                    {
                        try
                        {
                            var response = run(_httpClient, endpoint, cancellatonTokenSource.Token).Result;

                            var responseMessage = response as HttpResponseMessage;
                            if (responseMessage != null && !responseMessage.IsSuccessStatusCode)
                            {
                                if (ShouldTryOther(responseMessage))
                                {
                                    var exception = new HttpRequestException(responseMessage.ReasonPhrase);

                                    _endpointHealthIndicatorStore.DecreaseHealth(endpoint, exception);

                                    throw exception;
                                }
                                else
                                {
                                    cancellatonTokenSource.Cancel();
                                }
                            }

                            _endpointHealthIndicatorStore.IncreaseHealth(endpoint);

                            return response;
                        }
                        catch (Exception ex)
                        {
                            if (ShouldTryOther(ex))
                            {
                                if (!(ex is TaskCanceledException || ex.InnerException is TaskCanceledException))
                                {
                                    _endpointHealthIndicatorStore.DecreaseHealth(endpoint, ex);
                                }
                            }
                            else
                            {
                                cancellatonTokenSource.Cancel();
                            }
                            throw;
                        }
                    }, cancellatonTokenSource.Token));
            }

            return tasks;
        }

        private static bool ShouldTryOther(Exception ex)
        {
            var aex = ex as AggregateException;
            if (aex != null)
            {
                ex = aex.InnerExceptions.FirstOrDefault();
            }

            var wex = ex as WebException;
            if (wex == null)
            {
                wex = ex.InnerException as WebException;
            }
            if (wex != null && (
                wex.Status == WebExceptionStatus.UnknownError
                || wex.Status == WebExceptionStatus.ConnectFailure
                || (int)wex.Status == 1 // NameResolutionFailure
                ))
            {
                return true;
            }

            var reqex = ex as HttpRequestException;
            if (reqex != null)
            {
                return true;
            }

            if (ex is TaskCanceledException)
            {
                return true;
            }
            
            return false;
        }

        private static bool ShouldTryOther(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode
                || response.StatusCode == HttpStatusCode.BadGateway
                || response.StatusCode == HttpStatusCode.GatewayTimeout
                || response.StatusCode == HttpStatusCode.ServiceUnavailable
                || response.StatusCode == HttpStatusCode.RequestTimeout
                || response.StatusCode == HttpStatusCode.InternalServerError)
            {
                return true;
            }

            return false;
        }
        
        class WeightedRandomComparer
            : IComparer<int>
        {
            private readonly Random _random;

            public WeightedRandomComparer(Random random)
            {
                _random = random;
            }

            public int Compare(int x, int y)
            {
                var totalWeight = x + y;
                var randomNumber = _random.Next(0, totalWeight);

                if (randomNumber < x)
                {
                    return 1;
                }
                return -1;
            }
        }
    }
}