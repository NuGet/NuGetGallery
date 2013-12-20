using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NuGet.Services.Work
{
    [Serializable]
    public class UnknownJobException : Exception
    {
        public string JobName { get; private set; }

        public UnknownJobException(string jobName) : base(FormatMessage(jobName)) { JobName = jobName; }
        public UnknownJobException(string jobName, Exception inner) : base(FormatMessage(jobName), inner) { JobName = jobName; }
        protected UnknownJobException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) {
                JobName = info.GetString("JobName");
        }

        public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("JobName", JobName);
        }

        private static string FormatMessage(string jobName)
        {
            return String.Format(CultureInfo.CurrentCulture, Strings.UnknownJobException_Message, jobName);
        }
    }
}
