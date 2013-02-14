using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring
{
    public static class NetworkHelpers
    {
        private static readonly byte[] DefaultSendBuffer;

        static NetworkHelpers()
        {
            // "Borrowed" from the Ping class itself :)
            DefaultSendBuffer = new byte[0x20];
            for (int i = 0; i < 0x20; i++)
            {
                DefaultSendBuffer[i] = (byte)(0x61 + (i % 0x17));
            }
        }

        public static async Task<PingReply> Ping(string hostNameOrIPAddress, int ttl = 0)
        {
            var pinger = new Ping();
            var options = new PingOptions(ttl, dontFragment: true);
            
            return await pinger.SendTaskAsync(hostNameOrIPAddress, 0x1388, DefaultSendBuffer, options);
        }

        public static async Task<IEnumerable<IPAddress>> TraceRoute(string hostNameOrIPAddress)
        {
            List<IPAddress> addresses = new List<IPAddress>();
            await InternalTraceRoute(addresses, hostNameOrIPAddress, 1);
            return addresses;
        }

        private static async Task InternalTraceRoute(IList<IPAddress> list, string hostNameOrIPAddress, int currentTTL)
        {
            // Send a ping
            var reply = await Ping(hostNameOrIPAddress, currentTTL);

            // Check the result
            if (reply == null)
            {
                return; // Done
            }
            else if (reply.Status == IPStatus.Success)
            {
                // Found it!
                list.Add(reply.Address);
            }
            else if (reply.Status == IPStatus.TtlExpired)
            {
                // Got one hop
                list.Add(reply.Address);

                // Get the next hop recursively
                await InternalTraceRoute(list, hostNameOrIPAddress, currentTTL + 1);
            }
            else
            {
                // Failed for some other reason...
                Trace.WriteLine("Ping failed: " + reply.Status.ToString());
                return;
            }
        }
    }
}
