// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;
using System.Net;

namespace NuGetGallery.Auditing
{
    public static class Obfuscator
    {
        internal const string ObfuscatedUserName = "ObfuscatedUserName";

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
                var bytes = address.GetAddressBytes();
                //If the length 4 is IPV4 if the length is 16 IPV6 
                var length = bytes.Length;
                switch (length)
                {
                    case 4:
                        bytes[3] = 0;
                        break;
                    case 16:
                        for (int i = 8; i < 16; i++)
                        {
                            bytes[i] = 0;
                        }
                        break;
                    default:
                        break;
                }
                address = new IPAddress(bytes);
                return address.ToString();
            }
            return IP;
        }
    }
}
