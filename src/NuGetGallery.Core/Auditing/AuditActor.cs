// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Auditing
{
    public class AuditActor
    {
        public string MachineName { get; set; }
        public string MachineIP { get; set; }
        public string UserName { get; set; }
        public string AuthenticationType { get; set; }
        public DateTime TimestampUtc { get; set; }

        public AuditActor OnBehalfOf { get; set; }

        public AuditActor(string machineName, string machineIP, string userName, string authenticationType, DateTime timeStampUtc)
            : this(machineName, machineIP, userName, authenticationType, timeStampUtc, null) { }
        public AuditActor(string machineName, string machineIP, string userName, string authenticationType, DateTime timeStampUtc, AuditActor onBehalfOf)
        {
            MachineName = machineName;
            UserName = userName;
            AuthenticationType = authenticationType;
            TimestampUtc = timeStampUtc;
            OnBehalfOf = onBehalfOf;
        }

        public static Task<AuditActor> GetCurrentMachineActor()
        {
            return GetCurrentMachineActor(null);
        }

        public static async Task<AuditActor> GetCurrentMachineActor(AuditActor onBehalfOf)
        {
            // Try to get local IP
            string ipAddress = await GetLocalIP();

            return new AuditActor(
                Environment.MachineName,
                ipAddress,
                String.Format(@"{0}\{1}", Environment.UserDomainName, Environment.UserName),
                "MachineUser",
                DateTime.UtcNow,
                onBehalfOf);
        }

        public static async Task<string> GetLocalIP()
        {
            string ipAddress = null;
            if (NetworkInterface.GetIsNetworkAvailable())
            {
                var entry = await Dns.GetHostEntryAsync(Dns.GetHostName());
                if (entry != null)
                {
                    ipAddress =
                        TryGetAddress(entry.AddressList, AddressFamily.InterNetworkV6) ??
                        TryGetAddress(entry.AddressList, AddressFamily.InterNetwork);
                }
            }
            return ipAddress;
        }

        private static string TryGetAddress(IEnumerable<IPAddress> addrs, AddressFamily family)
        {
            return addrs.Where(a => a.AddressFamily == family).Select(a => a.ToString()).FirstOrDefault();
        }
    }
}
