using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.ContentScan
    {
    public class CheckContentScanStatusData
    {
        public CheckContentScanStatusData(
           Guid valiadtionStepId)
        {
            ValidationStepId = valiadtionStepId;
        }

        public Guid ValidationStepId { get; }
    }
}
