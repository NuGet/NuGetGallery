// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Search.Client
{
    public class HttpRetryMessageHandler : DelegatingHandler
    {
        private readonly Action<Exception> _onException;
        private readonly int _retryCount = 0;

        public HttpRetryMessageHandler(HttpClientHandler handler, Action<Exception> onException) : this(handler, onException, 0)
        {
        }

        public HttpRetryMessageHandler(HttpClientHandler handler, Action<Exception> onException, int retryCount) : this(handler, onException, retryCount, null)
        {
        }

        public HttpRetryMessageHandler(HttpClientHandler handler, Action<Exception> onException, int retryCount, IEnumerable<HttpStatusCode> additionalHttpStatusCodesWorthRetrying) : base(handler)
        {
            _onException = onException ?? throw new ArgumentNullException(nameof(onException));
            _retryCount = retryCount;
            if (additionalHttpStatusCodesWorthRetrying != null)
            {
                GetRetryingCodes.AddRange(additionalHttpStatusCodesWorthRetrying);
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            RetryPolicy<HttpResponseMessage>.
            HandleExceptionAndResult( (ex) => { _onException(ex); return false; }, r => GetRetryingCodes.Contains(r.StatusCode)).
            ExecuteWithWaitAndRetryAsync(_retryCount, retryAttempt => TimeSpan.FromSeconds(retryAttempt),
                () =>  
                base.SendAsync(request, cancellationToken) 
                );

        public List<HttpStatusCode> GetRetryingCodes { get; } = new List<HttpStatusCode>{
                HttpStatusCode.RequestTimeout, // 408
                HttpStatusCode.InternalServerError, // 500
                HttpStatusCode.BadGateway, // 502
                HttpStatusCode.ServiceUnavailable, // 503
                HttpStatusCode.GatewayTimeout // 504
            };

        public class RetryPolicy<TResult>
        {
            private Func<Exception, bool> _exceptionHandlerPredicate;
            private Func<TResult, bool> _resultHandlerPredicate;

            private RetryPolicy()
            { }

            public static RetryPolicy<TResult> HandleExceptionAndResult(Func<Exception, bool> exceptionHandlerPredicate, Func<TResult, bool> resultHandlerPredicate)
            {
                var policy = new RetryPolicy<TResult>();
                policy._exceptionHandlerPredicate = exceptionHandlerPredicate;
                policy._resultHandlerPredicate = resultHandlerPredicate;
                return policy;
            }

            /// <summary>
            /// It will execute retriesCount + 1.
            /// </summary>
            /// <param name="retriesCount">How many retries.</param>
            /// <param name="waitDurationProvider">A <see cref="Func{int, TimeSpan}"/> to calculate wait time between executions.</param>
            /// <param name="action">The action to execute.</param>
            /// <returns></returns>
            public async Task<TResult> ExecuteWithWaitAndRetryAsync(int retriesCount, Func<int, TimeSpan> waitDurationProvider, Func<Task<TResult>> action)
            {
                if(retriesCount < 0)
                {
                    throw new ArgumentException($"{nameof(retriesCount)} has to be greater or equal with zero.");
                }
                Func<Task<TResult>> asyncExecution = async () =>
                {
                    int initialRetryCount = retriesCount;
                    TResult result = default(TResult);
                    while (initialRetryCount >= 0)
                    {
                        try
                        {
                            result = await action();
                            if (_resultHandlerPredicate == null || !_resultHandlerPredicate(result) || initialRetryCount == 0)
                            {
                                return result;
                            }
                        }
                        catch (Exception ex)
                        {
                            if (_exceptionHandlerPredicate == null || !_exceptionHandlerPredicate(ex) || initialRetryCount == 0)
                            {
                                throw ex;
                            }
                        }
                        await Task.Delay(waitDurationProvider(initialRetryCount));
                        initialRetryCount--;
                    }

                    return result;
                };

                return await asyncExecution();
            }
        }
    }
}
