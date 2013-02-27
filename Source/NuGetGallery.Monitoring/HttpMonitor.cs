using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring
{
    public class HttpMonitor : ApplicationMonitor
    {
        /// <summary>
        /// The Url to Get
        /// </summary>
        public Uri Url { get; private set; }

        /// <summary>
        /// The expected status code. Specify 'null' to consider all 200-level codes as success.
        /// </summary>
        public HttpStatusCode? ExpectedStatusCode { get; private set; }

        /// <summary>
        /// Gets a boolean indicating if the SSL certificate for the page should be checked.
        /// </summary>
        public bool CheckCertificate { get; private set; }

        /// <summary>
        /// Gets or sets the time in milliseconds we expect to take to ping the site
        /// </summary>
        /// <remarks>
        /// If the monitor can't reach the site in this time, it reports Unhealthy and tries again with a long timeout. If it succeeds,
        /// it reports Degraded until the Timeout returns to normal.
        /// </remarks>
        public int ExpectedTimeout { get; set; }

        /// <summary>
        /// Gets or sets the maximum time in milliseconds we expect to take to ping the site
        /// </summary>
        /// <remarks>
        /// If the monitor can't reach the site in this time, it reports it as dead
        /// </remarks>
        public int MaximumTimeout { get; set; }

        /// <summary>
        /// Gets or sets the HTTP Method to use when requesting
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// Gets or sets the number of times to attempt the request before failing
        /// </summary>
        public int NumberOfAttempts { get; set; }

        /// <summary>
        /// Gets or sets a boolean indicating if the monitor should test a known-good site before failing to identify monitor-side issues.
        /// </summary>
        public bool CheckKnownGoodSite { get; set; }

        /// <summary>
        /// Gets or sets the URL to a known-good site that can be used to test network connectivity issues
        /// </summary>
        public Uri KnownGoodSite { get; set; }

        protected override string DefaultResourceName
        {
            get
            {
                return Url.AbsoluteUri;
            }
        }

        public HttpMonitor(string url) : this(new Uri(url)) { }
        public HttpMonitor(Uri url) : this(url, checkCertificate: false) { }

        public HttpMonitor(string url, HttpStatusCode expectedStatusCode) : this(new Uri(url), expectedStatusCode) { }
        public HttpMonitor(Uri url, HttpStatusCode expectedStatusCode) : this(url, expectedStatusCode, checkCertificate: false) { }

        public HttpMonitor(string url, bool checkCertificate) : this(new Uri(url)) { }
        public HttpMonitor(Uri url, bool checkCertificate)
        {
            Url = url;
            CheckCertificate = checkCertificate;
            ExpectedStatusCode = null;
            Method = "GET";
            NumberOfAttempts = 3;
            CheckKnownGoodSite = true;
            ExpectedTimeout = 200;
            MaximumTimeout = 5000;
            KnownGoodSite = new Uri("http://www.bing.com");
        }

        public HttpMonitor(string url, HttpStatusCode expectedStatusCode, bool checkCertificate) : this(new Uri(url), expectedStatusCode) { }
        public HttpMonitor(Uri url, HttpStatusCode expectedStatusCode, bool checkCertificate)
            : this(url, checkCertificate)
        {
            ExpectedStatusCode = expectedStatusCode;
        }

        protected override async Task Invoke()
        {
            FlushDnsCache();

            ServicePointManager.DnsRefreshTimeout = 0;
            if (CheckCertificate)
            {
                ServicePointManager.ServerCertificateValidationCallback = CertificateValidationCallBack;
            }

            // Try once with a short timeout, we definitely should be able to get there in 200ms
            var requestResult = await MakeSingleRequest(Url, Method, timeout: 200);
            if (requestResult.IsSuccess && IsSuccessfulResponse(requestResult, ExpectedStatusCode))
            {
                return;
            }

            // Try the known good site
            if (CheckKnownGoodSite)
            {
                var knownGoodResult = await MakeSingleRequest(KnownGoodSite, "GET", timeout: ExpectedTimeout);
                if (!knownGoodResult.IsSuccess || !IsSuccessfulResponse(knownGoodResult, HttpStatusCode.OK))
                {
                    // We can't reach the known-good site either. Report a monitor failure
                    MonitorFailure(String.Format("Failed to reach {0}, but couldn't reach {1} either. Network issues at monitor.",
                        Url.AbsoluteUri,
                        KnownGoodSite.AbsoluteUri));
                }
                else
                {
                    // We reached the known good site.
                    Unhealthy(String.Format("Failed to reach {0} in 200ms. Still confirming failure.", Url.AbsoluteUri));
                }
            }

            // If we get here, we failed the initial attempt, try a longer timeout and multiple attempts
            requestResult = await MakeMultipleRequests(Url, Method, NumberOfAttempts, timeout: MaximumTimeout);
            if (requestResult.IsSuccess && IsSuccessfulResponse(requestResult, ExpectedStatusCode))
            {
                Degraded(String.Format("Reached {0} in {1:N2}ms. This is longer than the expected timout of {2}ms.", Url.AbsoluteUri, requestResult.Time.TotalMilliseconds, ExpectedTimeout));
                return;
            }

            // Failed after multiple attempts, check the known good site with a longer timeout.
            if (CheckKnownGoodSite)
            {
                // Check a known good site
                var timing = await MakeSingleRequest(KnownGoodSite, "GET", timeout: MaximumTimeout); // If we can't reach it in MaximumTimeout, the monitor is really hosed
                if (!timing.IsSuccess || !IsSuccessfulResponse(timing, HttpStatusCode.OK))
                {
                    // We can't reach the known-good site either. Report a monitor failure
                    MonitorFailure(String.Format("Failed to reach {0}, but couldn't reach {1} either. Network issues at monitor.",
                        Url.AbsoluteUri,
                        KnownGoodSite.AbsoluteUri));
                    return;
                }
                else
                {
                    // Reached the known good site. Try the target one last time
                    requestResult = await MakeMultipleRequests(Url, Method, NumberOfAttempts, timeout: MaximumTimeout);
                }
            }

            // Final check
            if (requestResult.IsSuccess && IsSuccessfulResponse(requestResult, ExpectedStatusCode))
            {
                return;
            }

            // Phew, we're really sure we can't reach the target now
            await ReportFailure(requestResult);
        }

        private void FlushDnsCache()
        {
            NativeMethods.DnsFlushResolverCache();
        }

        private async Task<TimeResult> MakeMultipleRequests(Uri url, string method, int attempts, int timeout)
        {
            TimeResult lastResult = null;
            for (int i = 0; i < attempts; i++)
            {
                lastResult = await MakeSingleRequest(url, method, timeout);
                if (lastResult.IsSuccess && IsSuccessfulResponse(lastResult, ExpectedStatusCode))
                {
                    Success(String.Format("Successfully reached page in {0} attempts", i + 1));
                    QoS(String.Format("Requesting {0}", url.AbsolutePath), success: true, timeTaken: lastResult.Time);
                    return lastResult;
                }
            }
            return lastResult;
        }

        private async Task ReportFailure(TimeResult lastResult)
        {
            // Try to grab a trace route
            IEnumerable<IPAddress> traceRoute = null;
            try
            {
                traceRoute = await NetworkHelpers.TraceRoute(Url.Host);
            }
            catch (PingException pex)
            {
                // Report a monitor failure
                MonitorFailure(String.Format("Failed to collect trace route to {0}. {1}",
                    Url.AbsoluteUri,
                    pex));
            }

            if (!lastResult.IsSuccess)
            {
                Failure(String.Format("Definite Failure. Exception Requesting Page: {0}.\nTrace Route:\n{1}", lastResult.Exception.GetBaseException().Message, FormatTraceRoute(traceRoute)));
                return;
            }
            else if (!IsSuccessfulResponse(lastResult, ExpectedStatusCode))
            {
                var httpResult = lastResult as TimeResult<HttpWebResponse>;
                if (httpResult != null)
                {
                    Failure(String.Format("Definite Failure. HTTP Error: {0} {1}.\nTrace Route: {2}", httpResult.Result.StatusCode, httpResult.Result.StatusCode, FormatTraceRoute(traceRoute)));
                }
                return;
            }
        }

        private string FormatTraceRoute(IEnumerable<IPAddress> traceRoute)
        {
            if (traceRoute == null)
            {
                return "<Unknown>";
            }
            return String.Join(Environment.NewLine, traceRoute.Select((a, idx) => (idx + 1).ToString().PadRight(2) + ") " + a.ToString()));
        }

        private bool IsSuccessfulResponse(TimeResult result, HttpStatusCode? expectedStatusCode)
        {
            TimeResult<HttpWebResponse> httpResult = result as TimeResult<HttpWebResponse>;
            if (httpResult == null)
            {
                return false;
            }
            var response = httpResult.Result;

            var statusCode = (int)response.StatusCode;
            return (expectedStatusCode == null && statusCode >= 200 && statusCode < 300) ||
                (expectedStatusCode != null && ((int)expectedStatusCode.Value == statusCode));
        }

        private async Task<TimeResult> MakeSingleRequest(Uri url, string method, int timeout)
        {
            // Resolve the host
            var dnsResult = await Time(async () => 
                await Task.Factory
                    .FromAsync((cb, state) => Dns.BeginGetHostAddresses(url.Host, cb, state), res => Dns.EndGetHostAddresses(res), new object())
                    .TimeoutAfter(timeout, "DNS Resolution"));
            if (!dnsResult.IsSuccess)
            {
                return dnsResult;
            }

            var host = url.Host;
            url = (new UriBuilder(url) { Host = dnsResult.Result[0].ToString() }).Uri;

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Host = host;
            request.Method = method;
            request.KeepAlive = false;
            Trace.WriteLine(String.Format("http {0} [{2}] {1}", method, url.AbsoluteUri, host));

            return await Time(async () =>
            {
                HttpWebResponse response;
                try
                {
                    response = (HttpWebResponse)(await request.GetResponseAsync().TimeoutAfter(timeout, "Web Request"));
                }
                catch (WebException wex)
                {
                    response = wex.Response as HttpWebResponse;
                    if (response == null)
                    {
                        throw;
                    }
                }
                using (var input = response.GetResponseStream())
                using (var memoryStream = new MemoryStream())
                {
                    // Read the whole stream just to make sure we get all the data
                    await input.CopyToAsync(memoryStream);
                }
                Trace.WriteLine(String.Format("http {0} {1}", (int)response.StatusCode, url.AbsoluteUri));
                return response;
            });
        }

        private bool CertificateValidationCallBack(object sender,
                                                   X509Certificate certificate,
                                                   X509Chain chain,
                                                   SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors != SslPolicyErrors.None)
            {
                Failure(String.Format("Certificate errors: {0}", sslPolicyErrors.ToString()), Url.AbsoluteUri);
                return false;
            }

            DateTime expirationTime;
            if (DateTime.TryParse(certificate.GetExpirationDateString(), out expirationTime))
            {
                TimeSpan expiringIn = expirationTime - DateTime.UtcNow;
                if (expiringIn < TimeSpan.FromDays(10))
                {
                    Degraded(String.Format("Certificate is about to expire! Expiration Date: {0}", expirationTime), Url.AbsoluteUri);
                }
                Success(String.Format("Certificate is ok. Expiring in {0} days.", (int)expiringIn.TotalDays), Url.AbsoluteUri);
            }
            else
            {
                Failure(String.Format("Unable to parse certificate expiration date. Expiration value: " + certificate.GetExpirationDateString()));
            }
            return true;
        }
    }
}
