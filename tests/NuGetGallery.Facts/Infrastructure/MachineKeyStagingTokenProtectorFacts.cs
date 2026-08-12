// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Text;
using Xunit;

namespace NuGetGallery
{
    public class MachineKeyStagingTokenProtectorFacts
    {
        [Fact]
        public void EmitsQuerySafeTokens()
        {
            var protector = new MachineKeyStagingTokenProtector();
            var payload = Enumerable.Range(0, 256).Select(x => (byte)x).ToArray();

            var token = protector.Protect(payload);

            Assert.DoesNotContain('+', token);
            Assert.DoesNotContain('/', token);
            Assert.DoesNotContain('=', token);
        }

        [Fact]
        public void RoundTripsProtectedPayload()
        {
            var protector = new MachineKeyStagingTokenProtector();
            var payload = Encoding.UTF8.GetBytes("{\"o\":42,\"f\":\"group:net10-preview\",\"k\":1999}");

            var token = protector.Protect(payload);
            var restored = protector.Unprotect(token);

            Assert.Equal(payload, restored);
        }

        [Fact]
        public void RejectsTamperedToken()
        {
            var protector = new MachineKeyStagingTokenProtector();
            var token = protector.Protect(Encoding.UTF8.GetBytes("payload-to-protect"));
            var tampered = (token[0] == 'A' ? 'B' : 'A') + token.Substring(1);

            Assert.Null(protector.Unprotect(tampered));
        }
    }
}
