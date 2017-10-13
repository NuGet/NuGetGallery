// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Web;

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
            MachineIP = machineIP;
            UserName = userName;
            AuthenticationType = authenticationType;
            TimestampUtc = timeStampUtc;
            OnBehalfOf = onBehalfOf;
        }

        public static Task<AuditActor> GetAspNetOnBehalfOfAsync()
        {
            // Use HttpContext to build an actor representing the user performing the action
            var context = HttpContext.Current;
            if (context == null)
            {
                return Task.FromResult<AuditActor>(null);
            }

            return GetAspNetOnBehalfOfAsync(new HttpContextWrapper(context));
        }

        public static Task<AuditActor> GetAspNetOnBehalfOfAsync(HttpContextBase context)
        {
            // Try to identify the client IP using various server variables
            var clientIpAddress = context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if (string.IsNullOrEmpty(clientIpAddress)) // Try REMOTE_ADDR server variable
            {
                clientIpAddress = context.Request.ServerVariables["REMOTE_ADDR"];
            }

            if (string.IsNullOrEmpty(clientIpAddress)) // Try UserHostAddress property
            {
                clientIpAddress = context.Request.UserHostAddress;
            }

            if (!string.IsNullOrEmpty(clientIpAddress) && clientIpAddress.IndexOf(".", StringComparison.Ordinal) > 0)
            {
                clientIpAddress = clientIpAddress.Substring(0, clientIpAddress.LastIndexOf(".", StringComparison.Ordinal)) + ".0";
            }

            string user = null;
            string authType = null;
            if (context.User != null)
            {
                user = context.User.Identity.Name;
                authType = context.User.Identity.AuthenticationType;
            }

            return Task.FromResult(new AuditActor(
                null,
                clientIpAddress,
                user,
                authType,
                DateTime.UtcNow));
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