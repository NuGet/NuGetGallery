// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.TestServer
{
    /// <summary>
    /// This class allocates ports while ensuring that:
    /// 1. Ports that are permanently taken (or taken for the duration of the test) are not being attempted to be used.
    /// 2. Ports are not shared across different tests (but you can allocate two different ports in the same test).
    ///
    /// Gotcha: If another application grabs a port during the test, we have a race condition.
    /// </summary>
    public class PortReserver
    {
        private static ConcurrentDictionary<string, bool> PortLock = new ConcurrentDictionary<string, bool>();
        private readonly int _basePort;

        /// <summary>
        /// Initializes an instance of PortReserver.
        /// </summary>
        /// <param name="basePort">The base port for all request URL's. Defaults to system chosen available port,
        /// at or below `65535`.</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public PortReserver(int? basePort = null)
        {
            if (basePort is null)
            {
                // Port 0 means find an available port on the system.
                var tcpListener = new TcpListener(IPAddress.Loopback, port: 0);
                tcpListener.Start();
                basePort = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
                tcpListener.Stop();
            }
            else if (basePort <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(basePort), "The base port must be greater than zero.");
            }

            if (!basePort.HasValue)
            {
                throw new InvalidOperationException("Unable to find a port");
            }

            _basePort = basePort.Value;
        }

        public async Task<T> ExecuteAsync<T>(
            Func<int, CancellationToken, Task<T>> action,
            CancellationToken token)
        {
            int port = _basePort - 1;

            while (true)
            {
                token.ThrowIfCancellationRequested();

                port++;

                if (port > 65535)
                {
                    throw new InvalidOperationException("Exceeded port range");
                }

                // ListUsedTCPPort prevents port contention with other apps.
                if (!IsTcpPortAvailable(port))
                {
                    continue;
                }

                // WaitForLockAsync prevents port contention with this app.
                string portLockName = $"NuGet-Port-{port}";

                try
                {
                    if (PortLock.TryAdd(portLockName, true))
                    {
                        // Run the action within the lock
                        return await action(port, token);
                    }
                }
                catch (OverflowException)
                {
                    throw;
                }
                finally
                {
                    PortLock.TryRemove(portLockName, out _);
                }
            }
        }

        private static bool IsTcpPortAvailable(int port)
        {
            var tcpListener = new TcpListener(IPAddress.Loopback, port);
            try
            {
                tcpListener.Start();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
            finally
            {
                tcpListener.Stop();
            }
        }
    }
}
