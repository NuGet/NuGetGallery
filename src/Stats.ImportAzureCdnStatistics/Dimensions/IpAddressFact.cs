// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;

namespace Stats.ImportAzureCdnStatistics
{
    public class IpAddressFact
    {
        public IpAddressFact(string ipAddress)
        {

            IpAddress = ipAddress;

            IPAddress addr;
            if (IPAddress.TryParse(ipAddress, out addr))
            {
                IpAddressBytes = addr.GetAddressBytes();
            }
        }

        public int Id { get; set; }

        public string IpAddress { get; private set; }
        public byte[] IpAddressBytes { get; private set; }
    }
}