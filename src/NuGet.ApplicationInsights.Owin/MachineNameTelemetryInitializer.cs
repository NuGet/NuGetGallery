using System;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace NuGet.ApplicationInsights.Owin
{
    public class MachineNameTelemetryInitializer : ITelemetryInitializer
    {
        const string MachineNameProperty = "MachineName";

        private string _machineName;

        public MachineNameTelemetryInitializer()
        {
            try
            {
                _machineName = Environment.MachineName;
            }
            catch
            {
            }
        }

        public void Initialize(ITelemetry telemetry)
        {
            telemetry.Context.Properties[MachineNameProperty] = _machineName;
        }
    }
}
