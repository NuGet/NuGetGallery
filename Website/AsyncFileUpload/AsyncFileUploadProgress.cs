using System.Runtime.Serialization;
using System.Web.Script.Serialization;

namespace NuGetGallery.AsyncFileUpload
{
    [DataContract]
    public class AsyncFileUploadProgress
    {
        internal AsyncFileUploadProgress(int contentLength)
        {
            ContentLength = contentLength;
        }

        [ScriptIgnore]
        public int ContentLength
        {
            get;
            set;
        }

        [ScriptIgnore]
        public int TotalBytesRead
        {
            get;
            set;
        }

        [DataMember]
        public int Progress
        {
            get
            {
                return (int)((long)TotalBytesRead * 100 / ContentLength);
            }
            set
            {
                // [DataMember] requires a setter
            }
        }

        [DataMember]
        public string FileName
        {
            get;
            set;
        }

        [ScriptIgnore]
        public int BytesRemaining
        {
            get
            {
                return ContentLength - TotalBytesRead;
            }
        }
    }
}