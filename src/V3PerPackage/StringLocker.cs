// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.V3PerPackage
{
    public class StringLocker
    {
        private readonly object _semaphoresLock = new object();
        private readonly Dictionary<string, SemaphoreSlim> _semaphores = new Dictionary<string, SemaphoreSlim>();

        private readonly ILogger<StringLocker> _logger;

        public StringLocker(ILogger<StringLocker> logger)
        {
            _logger = logger;
        }

        private async Task<IDisposable> AcquireAsync(string semaphoreName, TimeSpan timeout)
        {
            SemaphoreSlim semaphore;
            lock (_semaphores)
            {
                if (!_semaphores.TryGetValue(semaphoreName, out semaphore))
                {
                    semaphore = new SemaphoreSlim(1);
                    _semaphores[semaphoreName] = semaphore;
                }
            }

            var acquired = await semaphore.WaitAsync(TimeSpan.Zero);
            if (!acquired)
            {
                _logger.LogInformation("The string {SemaphoreName} has already been acquired. Waiting.", semaphoreName);
                acquired = await semaphore.WaitAsync(timeout);
                if (!acquired)
                {
                    throw new TimeoutException($"Could not acquire a lock on string {semaphoreName} after waiting {timeout}.");
                }
            }
            
            return new DisposableSemaphoreSlim(semaphore);
        }

        /// <summary>
        /// This acquires a lock of a sequency of strings. To avoid deadlocks, the strings are deduplicated and sorted.
        /// </summary>
        /// <param name="semaphoreNames">The strings to lock.</param>
        /// <param name="timeout">
        /// A timeout to apply while acquiring each semaphore. This is not a timeout applied to all semaphores.
        /// </param>
        /// <returns>A disposable which releases all semaphores.</returns>
        public async Task<IDisposable> AcquireAsync(IEnumerable<string> semaphoreNames, TimeSpan timeout)
        {
            var disposables = new DisposableList();
            var semaphoreNameList = semaphoreNames.OrderBy(x => x).Distinct().ToList();

            try
            {
                foreach (var semaphoreName in semaphoreNameList)
                {
                    disposables.Add(await AcquireAsync(semaphoreName, timeout));
                }
            }
            catch
            {
                disposables.Dispose();
                throw;
            }

            return disposables;
        }

        private class DisposableList : List<IDisposable>, IDisposable
        {
            public void Dispose()
            {
                foreach (var disposable in this)
                {
                    disposable?.Dispose();
                }
            }
        }

        private class DisposableSemaphoreSlim : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;
            private int _state = (int)State.Live;

            public DisposableSemaphoreSlim(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
            }

            public void Dispose()
            {
                var originalState = (State)Interlocked.CompareExchange(ref _state, (int)State.Disposed, (int)State.Live);
                if (originalState == State.Disposed)
                {
                    return;
                }

                _semaphore.Release();
            }

            private enum State
            {
                Live,
                Disposed,
            }
        }
    }
}
