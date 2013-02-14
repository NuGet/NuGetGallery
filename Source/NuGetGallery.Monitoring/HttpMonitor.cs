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

            var lastResult = await MakeMultipleRequests(Url, Method, NumberOfAttempts);
            if(lastResult.IsSuccess && IsSuccessfulResponse(lastResult.Result, ExpectedStatusCode)) {
                return;
            }

            // If we get here, we failed every attempt
            if (CheckKnownGoodSite)
            {
                // Check a known good site
                var timing = await MakeSingleRequest(KnownGoodSite, "GET");
                if (!timing.IsSuccess || !IsSuccessfulResponse(timing.Result, HttpStatusCode.OK))
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
                    lastResult = await MakeMultipleRequests(Url, Method, NumberOfAttempts);
                }
            }

            // Final check
            if (lastResult.IsSuccess && IsSuccessfulResponse(lastResult.Result, ExpectedStatusCode))
            {
                return;
            }

            // Phew, we're really sure we can't reach the target now
            await ReportFailure(lastResult);
        }

        private void FlushDnsCache()
        {
            NativeMethods.DnsFlushResolverCache();
        }

        private async Task<TimeResult<HttpWebResponse>> MakeMultipleRequests(Uri url, string method, int attempts)
        {
            TimeResult<HttpWebResponse> lastResult = null;
            for (int i = 0; i < attempts; i++)
            {
                lastResult = await MakeSingleRequest(Url, method);
                if (lastResult.IsSuccess && IsSuccessfulResponse(lastResult.Result, ExpectedStatusCode))
                {
                    Success(String.Format("Successfully reached page in {0} attempts", i + 1));
                    QoS(String.Format("Requesting {0}", url.AbsolutePath), success: true, timeTaken: lastResult.Time);
                    return lastResult;
                }
            }
            return lastResult;
        }

        private async Task ReportFailure(TimeResult<HttpWebResponse> lastResult)
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
                Failure(String.Format("Exception Requesting Page: {0}.\nTrace Route:\n{1}", lastResult.Exception.Message, FormatTraceRoute(traceRoute)));
                return;
            }
            else if (!IsSuccessfulResponse(lastResult.Result, ExpectedStatusCode))
            {
                Failure(String.Format("HTTP Error: {0} {1}.\nTrace Route: {2}", lastResult.Result.StatusCode, lastResult.Result.StatusCode, FormatTraceRoute(traceRoute)));
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

        private bool IsSuccessfulResponse(HttpWebResponse response, HttpStatusCode? expectedStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            return (expectedStatusCode == null && statusCode >= 200 && statusCode < 300) ||
                (expectedStatusCode != null && ((int)expectedStatusCode.Value == statusCode));
        }

        private async Task<TimeResult<HttpWebResponse>> MakeSingleRequest(Uri url, string method)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.KeepAlive = false;
            Trace.WriteLine(String.Format("http {0} {1}", method, url.AbsoluteUri));
            
            return await Time(async () =>
            {
                HttpWebResponse response;
                try
                {
                    response = (HttpWebResponse)(await request.GetResponseAsync());
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
