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
        [DataMember]
        public int ContentLength
        {
            get;
            set;
        }

        [ScriptIgnore]
        [DataMember]
        public int TotalBytesRead
        {
            get;
            set;
        }

        [DataMember]
        public string FileName
        {
            get;
            set;
        }

        public int Progress
        {
            get
            {
                if (ContentLength == 0)
                {
                    return 0;
                }

                return (int)((long)TotalBytesRead * 100 / ContentLength);
            }
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