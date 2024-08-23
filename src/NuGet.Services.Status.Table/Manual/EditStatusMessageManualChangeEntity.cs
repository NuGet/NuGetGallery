using System;

namespace NuGet.Services.Status.Table.Manual
{
    public class EditStatusMessageManualChangeEntity : ManualStatusChangeEntity
    {
        public EditStatusMessageManualChangeEntity()
        {
        }

        public EditStatusMessageManualChangeEntity(
            string eventAffectedComponentPath,
            DateTime eventStartTime,
            DateTime messageTimestamp,
            string messageContents)
            : base(ManualStatusChangeType.EditStatusMessage)
        {
            EventAffectedComponentPath = eventAffectedComponentPath ?? throw new ArgumentNullException(nameof(eventAffectedComponentPath));
            EventStartTime = eventStartTime;
            MessageTimestamp = messageTimestamp;
            MessageContents = messageContents ?? throw new ArgumentNullException(nameof(messageContents));
        }

        public string EventAffectedComponentPath { get; set; }

        public DateTime EventStartTime { get; set; }

        public DateTime MessageTimestamp { get; set; }

        public string MessageContents { get; set; }
    }
}
