using System.Diagnostics;

namespace Tests.AppConfig.AzureJobTraceListener
{
    class Program
    {
        static void Main(string[] args)
        {
            Trace.TraceInformation("Program Information");
            Trace.TraceWarning("Program Warning");
            Trace.TraceError("Program error");
            Trace.Close();
        }
    }
}
