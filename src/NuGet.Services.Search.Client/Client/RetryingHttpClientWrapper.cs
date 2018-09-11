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
        private readonly Action<Exception> _onException;

        private static readonly int PeriodToDelayAlternateRequest = 3000;
        private static readonly IComparer<int> HealthComparer;

        static RetryingHttpClientWrapper()
        {
            HealthComparer = new WeightedRandomComparer();
        }

        public RetryingHttpClientWrapper(HttpClient httpClient, Action<Exception> onException)
            : this (httpClient, new BaseUrlHealthIndicatorStore(new NullHealthIndicatorLogger()), onException)
        {
        }

        public RetryingHttpClientWrapper(HttpClient httpClient, IEndpointHealthIndicatorStore endpointHealthIndicatorStore, Action<Exception> onException)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _endpointHealthIndicatorStore = endpointHealthIndicatorStore ?? throw new ArgumentNullException(nameof(endpointHealthIndicatorStore));
            _onException = onException ?? throw new ArgumentNullException(nameof(onException));
        }

        public async Task<string> GetStringAsync(IEnumerable<Uri> endpoints)
        {
            var response = await GetWithRetry(endpoints, _httpClient);
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<HttpResponseMessage> GetAsync(IEnumerable<Uri> endpoints)
        {
            return await GetWithRetry(endpoints, _httpClient);
        }

        private async Task<HttpResponseMessage> GetWithRetry(IEnumerable<Uri> endpoints, HttpClient httpClient)
        {
            // Build endpoints, ordered by health and their order of appearance.
            // Most traffic should go to the first endpoint if all is healthy.
            var endpointsAsList = endpoints.ToList();
            var healthyEndpoints = endpointsAsList.OrderByDescending(e =>
            {
                var health = _endpointHealthIndicatorStore.GetHealth(e);
                var order = endpointsAsList.IndexOf(e);

                return (health * health) / ((order + 1) * 80);
            }, HealthComparer).ToList();

            // Make all requests cancellable using this CancellationTokenSource
            var cancellationTokenSource = new CancellationTokenSource();

            // Create requests queue
            var tasks = CreateRequestQueue(healthyEndpoints, httpClient, cancellationTokenSource);

            // When the first successful task comes in, return it. If no successful tasks are returned, throw an AggregateException.
            var exceptions = new List<Exception>();

            var taskList = tasks.ToList();
            Task<HttpResponseMessage> completedTask;
            do
            {
                completedTask = await Task.WhenAny(taskList);
                taskList.Remove(completedTask);

                if (completedTask.Exception != null)
                {
                    exceptions.AddRange(completedTask.Exception.InnerExceptions);
                }
                else
                {
                    cancellationTokenSource.Cancel(false);
                }
            } while ((completedTask.IsFaulted || completedTask.IsCanceled) && taskList.Any());

            if (!cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel(false);
            }

            foreach (var exception in exceptions)
            {
                _onException(exception);
            }

            if (completedTask.IsFaulted || completedTask.IsCanceled)
            {
                var exceptionsToThrow = exceptions.Where(e => !(e is TaskCanceledException || e.InnerException is TaskCanceledException)).ToList();
                if (exceptionsToThrow.Count == 1)
                {
                    throw exceptionsToThrow.First();
                }
                throw new AggregateException(exceptions);
            }
            return await completedTask;
        }
        
        private IEnumerable<Task<HttpResponseMessage>> CreateRequestQueue(List<Uri> endpoints, HttpClient httpClient, CancellationTokenSource cancellationTokenSource)
        {
            // Queue up a series of requests. Make each request wait a little longer.
            for (var i = 0; i < endpoints.Count; i++)
            {
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    yield break;
                }

                var endpoint = endpoints[i];

                yield return Task.Delay(i * PeriodToDelayAlternateRequest, cancellationTokenSource.Token)
                    .ContinueWith(async task =>
                    {
                        try
                        {
                            var responseMessage = await httpClient.GetAsync(endpoint, cancellationTokenSource.Token);
                            
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
                                    cancellationTokenSource.Cancel();
                                }
                            }

                            _endpointHealthIndicatorStore.IncreaseHealth(endpoint);

                            return responseMessage;
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
                                cancellationTokenSource.Cancel();
                            }

                            throw;
                        }
                    }, TaskContinuationOptions.OnlyOnRanToCompletion).Unwrap();
            }
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
            if (response.StatusCode == HttpStatusCode.BadGateway
                || response.StatusCode == HttpStatusCode.GatewayTimeout
                || response.StatusCode == HttpStatusCode.ServiceUnavailable
                || response.StatusCode == HttpStatusCode.RequestTimeout
                || response.StatusCode == HttpStatusCode.InternalServerError)
            {
                return true;
            }

            return false;
        }

        public class WeightedRandomComparer
            : IComparer<int>
        {
            public int Compare(int x, int y)
            {
                var totalWeight = x + y;
                var randomNumber = ThreadSafeRandom.Next(0, totalWeight);

                if (randomNumber < x)
                {
                    return 1;
                }
                else if (randomNumber > x)
                {
                    return -1;
                }
                else
                {
                    return 0;
                }
            }
        }
    }
}