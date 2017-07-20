// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using Xunit;

namespace NuGetGallery.Helpers
{
    public class ReadMeHelperFacts
    {
        [Fact]
        public void HasReadMeNullFormDataFact()
        {
            Assert.False(ReadMeHelper.HasReadMe(null));

            ReadMeRequest readMeRequest = new ReadMeRequest
            {
                ReadMeType = ""
            };
            Assert.False(ReadMeHelper.HasReadMe(readMeRequest));
        }

        [Theory]
        [InlineData("helloworld", false)]
        [InlineData("http://www.github.com", true)]
        public void HasReadMeUrlFact(string url, bool hasReadMe)
        {

            ReadMeRequest readMeRequest = new ReadMeRequest
            {
                ReadMeType = "Url",
                ReadMeUrl = url
            };
            Assert.Equal(hasReadMe, ReadMeHelper.HasReadMe(readMeRequest));
        }

        [Fact]
        public void HasReadMeFilePathFact()
        {
            ReadMeRequest readMeRequest = new ReadMeRequest
            {
                ReadMeType = "File",
                ReadMeFile = null
            };
            Assert.False(ReadMeHelper.HasReadMe(readMeRequest));
        }

        [Fact]
        public void HasReadMeWritten()
        {
            ReadMeRequest readMeRequest = new ReadMeRequest
            {
                ReadMeType = "Written",
                ReadMeWritten = null
            };
            Assert.False(ReadMeHelper.HasReadMe(readMeRequest));
            readMeRequest.ReadMeWritten = "";
            Assert.False(ReadMeHelper.HasReadMe(readMeRequest));
            Mock<ReadMeRequest> mockRequest = new Mock<ReadMeRequest>();
            mockRequest.Setup(x => x.ReadMeWritten).Returns("foo");
            readMeRequest.ReadMeWritten = mockRequest.Object.ReadMeWritten;
            Assert.True(ReadMeHelper.HasReadMe(readMeRequest));
        }
    }
}
