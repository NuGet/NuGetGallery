using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.ContentScan
{
    public class CheckContentScanData
    {
        public static CheckContentScanData NewStartContentScanData(
           Guid validationTrackingId,
           Uri contentUrl)
        {
            return new CheckContentScanData(
                ContentScanOperationType.StartScan,
                startContentScan: new StartContentScanData(
                    validationTrackingId,
                    contentUrl),
                checkContentScanStatus: null,
                deliveryCount: 0);
        }

        public static CheckContentScanData NewCheckContentScanStatus(
          Guid validationTrackingId)
        {
            return new CheckContentScanData(
                ContentScanOperationType.CheckStatus,
                startContentScan: null,
                checkContentScanStatus: new CheckContentScanStatusData(
                    validationTrackingId),
                deliveryCount: 0);
        }

        internal CheckContentScanData(
           ContentScanOperationType type,
           StartContentScanData startContentScan,
           CheckContentScanStatusData checkContentScanStatus,
           int deliveryCount)
        {
            switch (type)
            {
                case ContentScanOperationType.StartScan:
                    if (startContentScan == null)
                    {
                        throw new ArgumentNullException(nameof(startContentScan));
                    }
                    break;
                case ContentScanOperationType.CheckStatus:
                    if (checkContentScanStatus == null)
                    {
                        throw new ArgumentNullException(nameof(checkContentScanStatus));
                    }
                    break;
                default:
                    throw new NotSupportedException($"The Content Scan validation message type '{type}' is not supported.");
            }

            var notNullCount = 0;
            notNullCount += startContentScan != null ? 1 : 0;
            notNullCount += checkContentScanStatus != null ? 1 : 0;

            if (notNullCount > 1)
            {
                throw new ArgumentException("There should be exactly one non-null data instance provided.");
            }

            Type = type;
            StartContentScan = startContentScan;
            DeliveryCount = deliveryCount;
        }

        public ContentScanOperationType Type { get; }
        public CheckContentScanStatusData CheckContentScanStatus { get; }
        public StartContentScanData StartContentScan { get; }
        public int DeliveryCount { get; }
    }
}
