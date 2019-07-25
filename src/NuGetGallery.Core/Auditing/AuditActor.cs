// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using NuGetGallery.Authentication;
using NuGetGallery.Auditing.Obfuscation;

namespace NuGetGallery.Auditing
{
    public class AuditActor
    {
        private static string _localIpAddress;
        private static DateTime _localIpAddressExpiration;
        private const int _localIpAddressExpirationInMinutes = 10;

        public string MachineName { get; set; }

        [Obfuscate(ObfuscationType.IP)]
        public string MachineIP { get; set; }

        [Obfuscate(ObfuscationType.UserName)]
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
            TimestampUtc = timeStampUtc;
            OnBehalfOf = onBehalfOf;
            CredentialKey = credentialKey;
        }

        public static Task<AuditActor> GetAspNetOnBehalfOfAsync()
        {
            // Use HttpContext to build an actor representing the user performing the action
            var context = HttpContext.Current;
            if (context == null)
            {
                // If we have no http context, use the machine context instead.
                return GetCurrentMachineActorAsync();
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

            clientIpAddress = Obfuscator.ObfuscateIp(clientIpAddress);

            string user = null;
            string authType = null;
            string credentialKey = null;

            if (context.User != null)
            {
                user = context.User.Identity.Name;
                authType = context.User.Identity.AuthenticationType;

                var claimsIdentity = context.User.Identity as ClaimsIdentity;
                credentialKey = claimsIdentity?.GetClaimOrDefault(NuGetClaims.CredentialKey);
            }

            return Task.FromResult(new AuditActor(
                null,
                clientIpAddress,
                user,
                authType,
                credentialKey,
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
                string.Empty,
                DateTime.UtcNow,
                onBehalfOf);
        }

        /// <summary>
        /// Get the local machine's IP address.
        /// Note that this method is cached because the IP shouldn't change frequently, and the
        /// GetIsNetworkAvailable call is expensive.
        /// </summary>
        public static async Task<string> GetLocalIpAddressAsync()
        {
            if (string.IsNullOrEmpty(_localIpAddress) || DateTime.UtcNow >= _localIpAddressExpiration)
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    var entry = await Dns.GetHostEntryAsync(Dns.GetHostName());
                    if (entry != null)
                    {
                        _localIpAddress =
                            TryGetAddress(entry.AddressList, AddressFamily.InterNetworkV6) ??
                            TryGetAddress(entry.AddressList, AddressFamily.InterNetwork);
                        _localIpAddressExpiration = DateTime.UtcNow.AddMinutes(_localIpAddressExpirationInMinutes);
                    }
                }
            }
            return _localIpAddress;
        }

        private static string TryGetAddress(IEnumerable<IPAddress> addrs, AddressFamily family)
        {
            return addrs.Where(a => a.AddressFamily == family).Select(a => a.ToString()).FirstOrDefault();
        }
    }
}