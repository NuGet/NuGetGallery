// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NuGetGallery.Auditing
{
    public class AuditActor
    {
        public string MachineName { get; set; }
        public string MachineIP { get; set; }
        public string UserName { get; set; }
        public string AuthenticationType { get; set; }
        public string CredentialKey { get; set; }
        public DateTime TimestampUtc { get; set; }

        public AuditActor OnBehalfOf { get; set; }

        public AuditActor(string machineName, string machineIP, string userName, string authenticationType, string credentialKey, DateTime timeStampUtc)
            : this(machineName, machineIP, userName, authenticationType, credentialKey, timeStampUtc, null) { }

        public AuditActor(string machineName, string machineIP, string userName, string authenticationType, string credentialKey, DateTime timeStampUtc, AuditActor onBehalfOf)
        {
            MachineName = machineName;
            MachineIP = machineIP;
            UserName = userName;
            AuthenticationType = authenticationType;
            CredentialKey = credentialKey;
            TimestampUtc = timeStampUtc;
            OnBehalfOf = onBehalfOf;
        }

        public static Task<AuditActor> GetCurrentMachineActorAsync()
        {
            return GetCurrentMachineActorAsync(null);
        }

        public static async Task<AuditActor> GetCurrentMachineActorAsync(AuditActor onBehalfOf)
        {
            // Try to get local IP
            string ipAddress = await GetLocalIpAddressAsync();

            return new AuditActor(
                Environment.MachineName,
                ipAddress,
                $@"{Environment.UserDomainName}\{Environment.UserName}",
                "MachineUser",
                string.Empty,
                DateTime.UtcNow,
                onBehalfOf);
        }

        public static async Task<string> GetLocalIpAddressAsync()
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