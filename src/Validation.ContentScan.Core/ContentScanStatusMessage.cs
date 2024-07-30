using System;

namespace NuGet.Jobs.Validation.ContentScan
{
    public class CheckContentScanStatusData
    {
        public CheckContentScanStatusData(
           Guid validationStepId)
        {
            ValidationStepId = validationStepId;
        }

        public Guid ValidationStepId { get; }
    }
}
