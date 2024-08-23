using System;

namespace NuGet.Services.Status.Table.Manual
{
    public class DeleteStatusEventManualChangeEntity : ManualStatusChangeEntity
    {
        public DeleteStatusEventManualChangeEntity()
        {
        }

        public DeleteStatusEventManualChangeEntity(
            string eventAffectedComponentPath,
            DateTime eventStartTime)
            : base(ManualStatusChangeType.DeleteStatusEvent)
        {
            EventAffectedComponentPath = eventAffectedComponentPath ?? throw new ArgumentNullException(nameof(eventAffectedComponentPath));
            EventStartTime = eventStartTime;
        }

        public string EventAffectedComponentPath { get; set; }

        public DateTime EventStartTime { get; set; }
    }
}
