using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.Messages
{
    public class TempDataErrorMessage : IGalleryMessage
    {
        public string PlainTextMessage => throw new NotImplementedException();

        public bool HasRawHtmlRepresentation => throw new NotImplementedException();

        public string RawHtmlMessage => throw new NotImplementedException();
    }
}