// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using System.Net;

namespace NuGetGallery.Auditing
{
    public static class Obfuscator
    {
        /// <summary>
        /// For IPv4 zero the last octet
        /// For IPv6 zero the last 4 sextets
        /// </summary>
        /// <param name="IP"></param>
        /// <returns></returns>
        public static string ObfuscateIp(string IP)
        {
            IPAddress address;
            if (IPAddress.TryParse(IP, out address))
            {
                StringBuilder obfuscatedIpAddress = new StringBuilder();
                var bytes = address.GetAddressBytes();
                //If the length 4 is IPV4 if the length is 16 IPV6 
                var length = bytes.Length;
                switch (length)
                {
                    case 4:
                        bytes[3] = 0;
                        foreach (byte b in bytes)
                        {
                            obfuscatedIpAddress.AppendFormat("{0}.", b);
                        }
                        return obfuscatedIpAddress.ToString().Trim('.');
                    case 16:
                        for (int i = 8; i < 16; i++)
                        {
                            bytes[i] = 0;
                        }
                        int index = 0;
                        foreach (byte b in bytes)
                        {
                            index++;
                            obfuscatedIpAddress.AppendFormat("{0:x2}", b);
                            if (index % 2 == 0)
                            {
                                obfuscatedIpAddress.Append(":");
                                index = 0;
                            }
                        }
                        return obfuscatedIpAddress.ToString().Trim(':'); ;
                    default:
                        return IP;
                }
            }
            return IP;
        }
    }
}
