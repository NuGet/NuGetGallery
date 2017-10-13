// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class AuditActorTests
    {
        [Fact]
        public void Constructor_WithoutOnBehalfOf_AcceptsNullValues()
        {
            var actor = new AuditActor(
                machineName: null,
                machineIP: null,
                userName: null,
                authenticationType: null,
                credentialKey: null,
                timeStampUtc: DateTime.MinValue);

            Assert.Null(actor.MachineName);
            Assert.Null(actor.MachineIP);
            Assert.Null(actor.UserName);
            Assert.Null(actor.AuthenticationType);
        }

        [Fact]
        public void Constructor_WithOnBehalfOf_AcceptsNullValues()
        {
            var actor = new AuditActor(machineName: null,
                machineIP: null,
                userName: null,
                authenticationType: null,
                credentialKey: null,
                timeStampUtc: DateTime.MinValue,
                onBehalfOf: null);

            Assert.Null(actor.MachineName);
            Assert.Null(actor.MachineIP);
            Assert.Null(actor.UserName);
            Assert.Null(actor.AuthenticationType);
            Assert.Null(actor.OnBehalfOf);
            Assert.Null(actor.CredentialKey);
        }

        [Fact]
        public void Constructor_WithoutOnBehalfOf_AcceptsEmptyStringValues()
        {
            var actor = new AuditActor(
                machineName: string.Empty,
                machineIP: string.Empty,
                userName: string.Empty,
                authenticationType: string.Empty,
                credentialKey: string.Empty,
                timeStampUtc: DateTime.MinValue);

            Assert.Equal(string.Empty, actor.MachineName);
            Assert.Equal(string.Empty, actor.MachineIP);
            Assert.Equal(string.Empty, actor.UserName);
            Assert.Equal(string.Empty, actor.AuthenticationType);
            Assert.Equal(string.Empty, actor.CredentialKey);
        }

        [Fact]
        public void Constructor_WithOnBehalfOf_AcceptsEmptyStringValues()
        {
            var actor = new AuditActor(
                machineName: string.Empty,
                machineIP: string.Empty,
                userName: string.Empty,
                authenticationType: string.Empty,
                credentialKey: string.Empty,
                timeStampUtc: DateTime.MinValue,
                onBehalfOf: null);

            Assert.Equal(string.Empty, actor.MachineName);
            Assert.Equal(string.Empty, actor.MachineIP);
            Assert.Equal(string.Empty, actor.UserName);
            Assert.Equal(string.Empty, actor.AuthenticationType);
            Assert.Equal(string.Empty, actor.CredentialKey);
        }

        [Fact]
        public void Constructor_WithoutOnBehalfOf_SetsProperties()
        {
            var actor = new AuditActor(
                machineName: "a",
                machineIP: "b",
                userName: "c",
                authenticationType: "d",
                credentialKey: "e",
                timeStampUtc: DateTime.MinValue);

            Assert.Equal("a", actor.MachineName);
            Assert.Equal("b", actor.MachineIP);
            Assert.Equal("c", actor.UserName);
            Assert.Equal("d", actor.AuthenticationType);
            Assert.Equal("e", actor.CredentialKey);
            Assert.Equal(DateTime.MinValue, actor.TimestampUtc);
        }

        [Fact]
        public void Constructor_WithOnBehalfOf_SetsProperties()
        {
            var onBehalfOfActor = new AuditActor(
                machineName: null,
                machineIP: null,
                userName: null,
                authenticationType: null,
                credentialKey: null,
                timeStampUtc: DateTime.MinValue);
            var actor = new AuditActor(
                machineName: "a",
                machineIP: "b",
                userName: "c",
                authenticationType: "d",
                credentialKey: "e",
                timeStampUtc: DateTime.MinValue,
                onBehalfOf: onBehalfOfActor);

            Assert.Equal("a", actor.MachineName);
            Assert.Equal("b", actor.MachineIP);
            Assert.Equal("c", actor.UserName);
            Assert.Equal("d", actor.AuthenticationType);
            Assert.Equal("e", actor.CredentialKey);
            Assert.Equal(DateTime.MinValue, actor.TimestampUtc);
            Assert.Same(onBehalfOfActor, actor.OnBehalfOf);
        }
       
        [Fact]
        public async Task GetCurrentMachineActorAsync_WithoutOnBehalfOf()
        {
            var actor = await AuditActor.GetCurrentMachineActorAsync();
            var expectedIpAddress = await AuditActor.GetLocalIpAddressAsync();

            Assert.NotNull(actor);
            Assert.Equal(Environment.MachineName, actor.MachineName);
            Assert.Equal(expectedIpAddress, actor.MachineIP);
            Assert.Equal($@"{Environment.UserDomainName}\{Environment.UserName}", actor.UserName);
            Assert.Equal("MachineUser", actor.AuthenticationType);
            Assert.InRange(actor.TimestampUtc, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
            Assert.Null(actor.OnBehalfOf);
        }

        [Fact]
        public async Task GetCurrentMachineActorAsync_WithOnBehalfOf_AcceptsNull()
        {
            var expectedResult = await AuditActor.GetCurrentMachineActorAsync();
            var actualResult = await AuditActor.GetCurrentMachineActorAsync(onBehalfOf: null);

            Assert.NotNull(expectedResult);
            Assert.NotNull(actualResult);
            Assert.Equal(expectedResult.MachineName, actualResult.MachineName);
            Assert.Equal(expectedResult.MachineIP, actualResult.MachineIP);
            Assert.Equal(expectedResult.UserName, actualResult.UserName);
            Assert.Equal(expectedResult.AuthenticationType, actualResult.AuthenticationType);
            Assert.InRange(actualResult.TimestampUtc, expectedResult.TimestampUtc, expectedResult.TimestampUtc.AddMinutes(1));
            Assert.Null(actualResult.OnBehalfOf);
        }

        [Fact]
        public async Task GetLocalIpAddressAsync_ReturnsAppropriateValueForLocalMachine()
        {
            string expectedIpAddress = null;

            if (NetworkInterface.GetIsNetworkAvailable())
            {
                var entry = await Dns.GetHostEntryAsync(Dns.GetHostName());

                if (entry != null)
                {
                    expectedIpAddress =
                        TryGetAddress(entry.AddressList, AddressFamily.InterNetworkV6) ??
                        TryGetAddress(entry.AddressList, AddressFamily.InterNetwork);
                }
            }

            var actualIpAddress = await AuditActor.GetLocalIpAddressAsync();

            Assert.Equal(expectedIpAddress, actualIpAddress);
        }

        private static string TryGetAddress(IEnumerable<IPAddress> addresses, AddressFamily family)
        {
            return addresses.Where(address => address.AddressFamily == family)
                            .Select(address => address.ToString())
                            .FirstOrDefault();
        }
    }
}