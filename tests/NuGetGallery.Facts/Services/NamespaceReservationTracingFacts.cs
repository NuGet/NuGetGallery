// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Entity.Infrastructure;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core.Pipeline;
using Azure.Monitor.OpenTelemetry.Exporter;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;
using static NuGetGallery.NamespaceReservationToolCallingFacts;

namespace NuGetGallery
{
    public class NamespaceReservationTracingFacts
    {
        private const string Accounts = "get_request_account_facts";
        private const string ProjectEndpoint = "https://synthetic.services.ai.azure.com/api/projects/local-poc";

        [Fact]
        public void ConfigurationDefaultsAreOffAndEndpointIsNull()
        {
            var configuration = new AppConfiguration();
            Assert.False(configuration.NamespaceReservationTracingEnabled);
            Assert.False(configuration.NamespaceReservationTracingCaptureContent);
            Assert.Null(configuration.NamespaceReservationTracingProjectEndpoint);
            foreach (var name in new[] { nameof(AppConfiguration.NamespaceReservationTracingEnabled), nameof(AppConfiguration.NamespaceReservationTracingCaptureContent) })
            {
                var property = TypeDescriptor.GetProperties(configuration)[name];
                Assert.Equal(false, ((DefaultValueAttribute)property.Attributes[typeof(DefaultValueAttribute)]).Value);
            }
        }

        [Theory]
        [InlineData(false, "Development", ProjectEndpoint, "disabled")]
        [InlineData(true, "Production", ProjectEndpoint, "not_development")]
        [InlineData(true, null, ProjectEndpoint, "not_development")]
        [InlineData(true, "Development", null, "invalid_project_endpoint")]
        [InlineData(true, "Development", "https://example.test/api/projects/poc", "invalid_project_endpoint")]
        public void DisallowedConfigurationNeverReadsEnvironmentOrCreatesExporter(bool enabled, string environment, string endpoint, string status)
        {
            var config = Configuration();
            config.NamespaceReservationTracingEnabled = enabled;
            config.Environment = environment;
            config.NamespaceReservationTracingProjectEndpoint = endpoint;
            var reads = 0;
            var factories = 0;
            using (var tracing = NamespaceReservationTracing.Create(config,
                (name, target) => { reads++; return SyntheticConnectionString(); },
                (source, connection) => { factories++; return new Disposable(); }))
            {
                Assert.False(tracing.IsEnabled);
                Assert.Equal(status, tracing.ConfigurationStatus);
                Assert.Equal(0, reads);
                Assert.Equal(0, factories);
            }
        }

        [Theory]
        [InlineData("http://synthetic.services.ai.azure.com/api/projects/poc")]
        [InlineData("https://synthetic.services.ai.azure.com:444/api/projects/poc")]
        [InlineData("https://user:password@synthetic.services.ai.azure.com/api/projects/poc")]
        [InlineData("https://synthetic.services.ai.azure.com/api/projects/poc?secret=private")]
        [InlineData("https://synthetic.services.ai.azure.com/api/projects/poc#private")]
        [InlineData("https://synthetic.services.ai.azure.com/api/projects/poc/")]
        [InlineData("https://synthetic.services.ai.azure.com/openai/v1")]
        [InlineData("https://synthetic.services.ai.azure.com.evil.test/api/projects/poc")]
        [InlineData("https://synthetic.services.ai.azure.com/api/projects/%70oc")]
        [InlineData("https://synthetic.services.ai.azure.com/api/other/../projects/poc")]
        public void ProjectEndpointMustBeExact(string endpoint)
        {
            Assert.False(NamespaceReservationTracing.IsProjectEndpoint(endpoint));
            Assert.True(NamespaceReservationTracing.IsProjectEndpoint(ProjectEndpoint));
        }

        [Fact]
        public void MissingDedicatedVariableDoesNotUseGeneralOrWorkSettings()
        {
            var reads = new List<EnvironmentVariableTarget>();
            var factories = 0;
            var config = Configuration();
            config.AppInsightsInstrumentationKey = Guid.NewGuid().ToString();
            using (var tracing = NamespaceReservationTracing.Create(config, (name, target) =>
            {
                // Simulate a populated global environment but an absent dedicated destination.
                reads.Add(target);
                return name == NamespaceReservationTracing.ConnectionStringVariable ? null : SyntheticConnectionString();
            }, (source, connection) => { factories++; return new Disposable(); }))
            {
                Assert.False(tracing.IsEnabled);
                Assert.Equal("missing_dedicated_connection_string", tracing.ConfigurationStatus);
                Assert.Equal(0, factories);
                Assert.Equal(Environment.OSVersion.Platform == PlatformID.Win32NT
                    ? new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User }
                    : new[] { EnvironmentVariableTarget.Process }, reads);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void UsesOnlyExplicitDestinationInProcessThenUserOrderAndOwnsProvider(bool useUser)
        {
            var connectionString = SyntheticConnectionString();
            var reads = new List<EnvironmentVariableTarget>();
            var provider = new Disposable();
            var factoryCalls = 0;
            using (var tracing = NamespaceReservationTracing.Create(Configuration(), (name, target) =>
            {
                Assert.Equal(NamespaceReservationTracing.ConnectionStringVariable, name);
                reads.Add(target);
                return useUser && target == EnvironmentVariableTarget.Process ? null : connectionString;
            }, (source, connection) =>
            {
                factoryCalls++;
                Assert.Equal(NamespaceReservationTracing.SourceName, source);
                Assert.DoesNotContain("*", source);
                Assert.Equal(connectionString, connection);
                return provider;
            }))
            {
                var supported = !useUser || Environment.OSVersion.Platform == PlatformID.Win32NT;
                Assert.Equal(supported, tracing.IsEnabled);
                Assert.Equal(supported ? 1 : 0, factoryCalls);
                Assert.Equal(EnvironmentVariableTarget.Process, reads[0]);
                Assert.DoesNotContain(EnvironmentVariableTarget.Machine, reads);
                Assert.False(provider.Disposed);
            }

            Assert.Equal(factoryCalls == 1, provider.Disposed);
        }

        [Theory]
        [InlineData("not a connection string")]
        [InlineData("InstrumentationKey=not-a-guid")]
        [InlineData("InstrumentationKey=00000000-0000-0000-0000-000000000000")]
        [InlineData("AccountKey=PRIVATE")]
        public void MalformedDestinationNeverConstructsExporter(string value)
        {
            var calls = 0;
            using (var tracing = NamespaceReservationTracing.Create(Configuration(), (name, target) => value,
                (source, connection) => { calls++; return new Disposable(); }))
            {
                Assert.False(tracing.IsEnabled);
                Assert.InRange(tracing.ConfigurationStatus.Length, 1, 64);
                Assert.DoesNotContain("PRIVATE", tracing.ConfigurationStatus);
                Assert.Equal(0, calls);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ConfigurationAndExporterFailuresSafelyDisable(bool factoryFailure)
        {
            using (var tracing = NamespaceReservationTracing.Create(Configuration(), (name, target) =>
            {
                if (!factoryFailure)
                {
                    throw new InvalidOperationException("PRIVATE environment secret");
                }

                return SyntheticConnectionString();
            }, (source, connection) => throw new InvalidOperationException("PRIVATE exporter secret")))
            {
                Assert.False(tracing.IsEnabled);
                Assert.Equal("initialization_failed", tracing.ConfigurationStatus);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CorrelatesModelAndToolSequenceAndRecordsRecommendationBeforeGuard(bool captureContent)
        {
            using (var recording = new Recording(captureContent))
            using (var fixture = new Fixture(recording.Tracing))
            {
                var first = ToolTurn(Accounts, "call_one");
                first["id"] = "resp_one";
                first["model"] = "test-model";
                ((JArray)first["output"]).Insert(0, new JObject
                {
                    ["type"] = "reasoning", ["encrypted_content"] = "PRIVATE_ENCRYPTED_REASONING", ["summary"] = new JArray()
                });
                fixture.Handler.Enqueue(first);
                fixture.Handler.Enqueue(Final(Decision("Approved", new[] { Accounts })));

                Assert.Equal("Rejected", (await fixture.AssessAsync()).Status);

                var root = Assert.Single(recording.Activities.Where(a => a.OperationName.StartsWith("invoke_agent ", StringComparison.Ordinal)));
                var children = recording.Activities.Where(a => a != root).ToArray();
                Assert.Equal(new[] { "chat test-deployment", "execute_tool " + Accounts, "chat test-deployment" }, children.Select(a => a.OperationName));
                Assert.All(children, child =>
                {
                    Assert.Equal(root.TraceId, child.TraceId);
                    Assert.Equal(root.SpanId, child.ParentSpanId);
                });
                Assert.Equal("Approved", root.GetTagItem("namespace_reservation.model.recommendation"));
                Assert.Equal("Rejected", root.GetTagItem("namespace_reservation.guard.accepted"));
                Assert.Equal(true, root.GetTagItem("namespace_reservation.guard.overridden"));
                Assert.Equal(ActivityStatusCode.Unset, root.Status);
                Assert.Equal(new[] { "model_recommendation", "guard_accepted_assessment" }, root.Events.Select(e => e.Name));
                Assert.Equal("0.9.0", root.GetTagItem("namespace_reservation.skill.version"));
                Assert.Equal(64, ((string)root.GetTagItem("namespace_reservation.skill.sha256")).Length);
                Assert.Equal("invoke_agent", root.GetTagItem("gen_ai.operation.name"));
                Assert.Equal("namespace-reservation-assessor", root.GetTagItem("gen_ai.agent.name"));
                Assert.Equal(1, children[0].GetTagItem("namespace_reservation.model.turn"));
                Assert.Equal(2, children[2].GetTagItem("namespace_reservation.model.turn"));
                Assert.Equal(200, children[0].GetTagItem("http.response.status_code"));
                Assert.Equal(13L, children[0].GetTagItem("gen_ai.usage.input_tokens"));
                Assert.Equal(7L, children[0].GetTagItem("gen_ai.usage.output_tokens"));
                Assert.Equal("resp_one", children[0].GetTagItem("gen_ai.response.id"));
                Assert.Equal("test-model", children[0].GetTagItem("gen_ai.response.model"));
                Assert.Equal("call_one", children[1].GetTagItem("gen_ai.tool.call.id"));
                Assert.Equal(1, children[1].GetTagItem("namespace_reservation.tool.attempt"));
                Assert.Equal(1, children[1].GetTagItem("namespace_reservation.tools.used"));
                var text = AllTags(recording);
                Assert.DoesNotContain("PRIVATE_ENCRYPTED_REASONING", text);
                Assert.DoesNotContain("instructions", text);
                Assert.DoesNotContain("submitterKey", text);
                if (captureContent)
                {
                    Assert.Contains("Example.Product", (string)children[0].GetTagItem("gen_ai.input.messages"));
                    Assert.DoesNotContain("Example.Product", (string)children[2].GetTagItem("gen_ai.input.messages"));
                    Assert.Equal("tool_call_response", (string)JArray.Parse((string)children[2].GetTagItem("gen_ai.input.messages"))[0]["parts"][0]["type"]);
                    Assert.Equal("{}", children[1].GetTagItem("gen_ai.tool.call.arguments"));
                }
                else
                {
                    AssertNoContent(recording);
                }
            }
        }

        [Theory]
        [InlineData(ActivityIdFormat.W3C, false)]
        [InlineData(ActivityIdFormat.Hierarchical, false)]
        [InlineData(ActivityIdFormat.W3C, true)]
        [InlineData(ActivityIdFormat.Hierarchical, true)]
        public async Task AssessmentHasDiscoverableRootWhenGalleryParentIsNotExported(ActivityIdFormat parentFormat, bool useSdk)
        {
            using (var parent = new Activity("Gallery HTTP request").SetIdFormat(parentFormat).Start())
            using (var recording = new Recording(false, useSdk))
            using (var fixture = new Fixture(recording.Tracing))
            {
                fixture.Handler.Enqueue(ToolTurn(Accounts, "call_one"));
                fixture.Handler.Enqueue(Final(Decision("Rejected", new[] { Accounts })));
                await fixture.AssessAsync();

                Assert.Same(parent, Activity.Current);
                Assert.DoesNotContain(parent, recording.Activities);
                var root = Assert.Single(recording.Activities.Where(a => a.ParentSpanId == default(ActivitySpanId)));
                Assert.Equal("invoke_agent namespace-reservation-assessor", root.OperationName);
                Assert.Equal(ActivityIdFormat.W3C, root.IdFormat);
                Assert.Null(root.Parent);
                Assert.True(string.IsNullOrEmpty(root.ParentId));
                Assert.NotEqual(parent.TraceId, root.TraceId);
                Assert.NotEqual(default(ActivityTraceId), root.TraceId);

                // Foundry first selects GenAI operation IDs, then inner-joins to an exported root.
                // Content events are optional and must not be needed for trace discovery.
                var genAiOperations = recording.Activities
                    .Where(a => a.GetTagItem("gen_ai.system") != null || a.GetTagItem("gen_ai.provider.name") != null)
                    .Select(a => a.TraceId)
                    .Distinct();
                Assert.Equal(root.TraceId, Assert.Single(genAiOperations));
                Assert.All(recording.Activities.Where(a => a != root), child =>
                {
                    Assert.Equal(root.TraceId, child.TraceId);
                    Assert.Equal(root.SpanId, child.ParentSpanId);
                });
                AssertNoContent(recording);
            }
        }

        [Theory]
        [InlineData(ActivityIdFormat.W3C)]
        [InlineData(ActivityIdFormat.Hierarchical)]
        public void ExportedAssessmentPassesFoundryRootFilter(ActivityIdFormat parentFormat)
        {
            using (var parent = new Activity("Gallery HTTP request").SetIdFormat(parentFormat).Start())
            using (var handler = new RecordingTelemetryHandler())
            using (var client = new HttpClient(handler))
            {
                var source = new ActivitySource("NamespaceTracingFacts." + Guid.NewGuid().ToString("N"));
                var provider = Sdk.CreateTracerProviderBuilder()
                    .SetResourceBuilder(ResourceBuilder.CreateEmpty())
                    .SetSampler(new AlwaysOnSampler())
                    .AddSource(source.Name)
                    .AddAzureMonitorTraceExporter(options =>
                    {
                        options.ConnectionString = SyntheticConnectionString() + ";IngestionEndpoint=https://synthetic.invalid/";
                        options.Transport = new HttpClientTransport(client);
                        options.DisableOfflineStorage = true;
                        options.Diagnostics.IsLoggingEnabled = false;
                        options.Diagnostics.IsLoggingContentEnabled = false;
                        options.Diagnostics.IsDistributedTracingEnabled = false;
                    })
                    .Build();
                using (var tracing = new NamespaceReservationTracing(source, false, provider))
                {
                    using (var scope = tracing.StartAssessment())
                    {
                        using (var model = tracing.StartModel("test-deployment", 1, 1024, "auto"))
                        {
                            model.Complete();
                        }

                        using (var tool = tracing.StartTool(Accounts, "call_one", 1))
                        {
                            tool.Complete();
                        }

                        scope.Complete();
                    }

                    Assert.Same(parent, Activity.Current);
                    Assert.True(provider.ForceFlush());
                    Assert.Equal(3, handler.Envelopes.Count);
                    var envelope = Assert.Single(handler.Envelopes.Where(e => (string)e["data"]["baseData"]["name"] == "invoke_agent namespace-reservation-assessor"));
                    var tags = (JObject)envelope["tags"];
                    Assert.True(string.IsNullOrEmpty((string)tags["ai.operation.parentId"]));
                    Assert.NotEqual(parent.TraceId.ToHexString(), (string)tags["ai.operation.id"]);
                    Assert.Equal("invoke_agent namespace-reservation-assessor", (string)envelope["data"]["baseData"]["name"]);
                    Assert.Equal("invoke_agent", (string)envelope["data"]["baseData"]["properties"]["gen_ai.operation.name"]);
                    Assert.All(handler.Envelopes.Where(e => e != envelope), child =>
                    {
                        Assert.Equal((string)tags["ai.operation.id"], (string)child["tags"]["ai.operation.id"]);
                        Assert.Equal((string)envelope["data"]["baseData"]["id"], (string)child["tags"]["ai.operation.parentId"]);
                    });
                }
            }
        }

        [Fact]
        public void RootWithoutListenerRestoresAmbientActivity()
        {
            using (var parent = new Activity("Gallery HTTP request").SetIdFormat(ActivityIdFormat.W3C).Start())
            using (var tracing = new NamespaceReservationTracing(new ActivitySource("NamespaceTracingFacts." + Guid.NewGuid().ToString("N")), false))
            using (var scope = tracing.StartAssessment())
            {
                Assert.Same(parent, Activity.Current);
                scope.Complete();
            }
        }

        [Fact]
        public void NestedAssessmentReusesRootAndRestoresGalleryContext()
        {
            using (var parent = new Activity("Gallery HTTP request").SetIdFormat(ActivityIdFormat.W3C).Start())
            using (var recording = new Recording(false))
            {
                using (var outer = recording.Tracing.StartAssessment())
                {
                    var root = Activity.Current;
                    using (var nested = recording.Tracing.StartAssessment())
                    {
                        Assert.Same(root, Activity.Current);
                        nested.Complete();
                    }

                    Assert.Same(root, Activity.Current);
                    Assert.Empty(recording.Activities);
                    outer.Complete();
                }

                Assert.Same(parent, Activity.Current);
                Assert.Equal(default(ActivitySpanId), Assert.Single(recording.Activities).ParentSpanId);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task BothOverloadsProduceOneRootAndPolicyRejectionIsNotTechnicalError(bool useEvidence)
        {
            using (var recording = new Recording(true))
            using (var fixture = new Fixture(recording.Tracing))
            {
                fixture.Handler.Enqueue(Final(Decision("Rejected")));
                var result = useEvidence ? await fixture.AssessAsync()
                    : await fixture.Client.AssessAsync(CreateInput(), CancellationToken.None);

                Assert.Equal("Rejected", result.Status);
                Assert.Equal(2, recording.Activities.Count);
                Assert.Equal(true, Assert.Single(recording.Activities.Where(a => a.OperationName.StartsWith("invoke_agent ", StringComparison.Ordinal)))
                    .GetTagItem("namespace_reservation.assessment_only"));
                Assert.All(recording.Activities, a => Assert.NotEqual(ActivityStatusCode.Error, a.Status));
            }
        }

        [Fact]
        public async Task ConnectionTestTracesModelCallWithoutNetworkOrCustomerContent()
        {
            using (var recording = new Recording(true))
            using (var fixture = new Fixture(recording.Tracing))
            {
                fixture.Handler.Enqueue(Final(Decision()));
                await fixture.Client.TestConnectionAsync(CancellationToken.None);
                var activity = Assert.Single(recording.Activities);
                Assert.Equal("chat test-deployment", activity.OperationName);
                Assert.Equal(200, activity.GetTagItem("http.response.status_code"));
                Assert.Null(activity.GetTagItem("gen_ai.input.messages"));
            }
        }

        [Fact]
        public void WebSearchTelemetryRecordsOnlyStructuralUsage()
        {
            using (var recording = new Recording(false))
            {
                using (var model = recording.Tracing.StartModel("test-deployment", 1, 1024, "auto", webSearchEnabled: true))
                {
                    model.Response(new JObject
                    {
                        ["id"] = "response_one",
                        ["model"] = "test-model",
                        ["output"] = new JArray(new JObject
                        {
                            ["type"] = "web_search_call",
                            ["status"] = "completed",
                            ["action"] = new JObject
                            {
                                ["type"] = "open_page",
                                ["url"] = "https://PRIVATE.example.test/product",
                                ["query"] = "PRIVATE customer search"
                            }
                        }),
                        ["tool_usage"] = new JObject
                        {
                            ["web_search"] = new JObject { ["num_requests"] = 2 }
                        }
                    });
                    model.Complete();
                }

                var activity = Assert.Single(recording.Activities);
                Assert.Equal(true, activity.GetTagItem("namespace_reservation.web_search.enabled"));
                Assert.Equal(true, activity.GetTagItem("namespace_reservation.web_search.called"));
                Assert.Equal(true, activity.GetTagItem("namespace_reservation.web_search.page_opened"));
                Assert.Equal(1, activity.GetTagItem("namespace_reservation.web_search.action_count"));
                Assert.Equal(2, activity.GetTagItem("namespace_reservation.web_search.request_count"));
                Assert.DoesNotContain("PRIVATE", AllTags(recording));
            }
        }

        [Theory]
        [InlineData("unknown", "{}", false)]
        [InlineData(Accounts, "{\"submitterKey\":987654}", false)]
        [InlineData(Accounts, "{}", true)]
        public async Task InvalidArgumentsUnknownToolsAndProviderFailureAreTracedWithoutRawContent(string name, string arguments, bool providerFailure)
        {
            using (var recording = new Recording(true))
            using (var fixture = new Fixture(recording.Tracing))
            {
                fixture.Handler.Enqueue(ToolTurn(name, "call_failure", arguments));
                fixture.Handler.Enqueue(Final(Decision()));
                if (providerFailure)
                {
                    fixture.Evidence.Read = (tool, args, token) => throw new InvalidOperationException("PRIVATE provider password");
                }

                Assert.Equal("Rejected", (await fixture.AssessAsync()).Status);
                var toolSpan = Assert.Single(recording.Activities.Where(a => a.OperationName.StartsWith("execute_tool ", StringComparison.Ordinal)));
                Assert.Equal("execute_tool " + (name == Accounts ? Accounts : "unknown"), toolSpan.OperationName);
                Assert.Equal(ActivityStatusCode.Error, toolSpan.Status);
                Assert.Equal(false, toolSpan.GetTagItem("namespace_reservation.tool.complete"));
                Assert.Equal(false, toolSpan.GetTagItem("namespace_reservation.operation.complete"));
                if (!providerFailure)
                {
                    Assert.Null(toolSpan.GetTagItem("gen_ai.tool.call.arguments"));
                }

                Assert.DoesNotContain("987654", AllTags(recording));
                Assert.DoesNotContain("PRIVATE", AllTags(recording));
                Assert.Equal(2, fixture.Handler.Requests.Count);
            }
        }

        [Fact]
        public async Task HttpFailureRecordsStatusAndSafeErrorWithoutExceptionMessages()
        {
            using (var recording = new Recording(true))
            using (var fixture = new Fixture(recording.Tracing))
            {
                fixture.Handler.Status = HttpStatusCode.InternalServerError;
                fixture.Handler.Responses.Enqueue("PRIVATE HTTP response");
                await Assert.ThrowsAsync<Azure.RequestFailedException>(() => fixture.AssessAsync());
                Assert.All(recording.Activities, a => Assert.Equal(ActivityStatusCode.Error, a.Status));
                Assert.Equal(500, recording.Activities[0].GetTagItem("http.response.status_code"));
                Assert.DoesNotContain("PRIVATE", AllTags(recording));
                Assert.DoesNotContain("exception.message", AllTags(recording));
                Assert.DoesNotContain("exception.stacktrace", AllTags(recording));
            }
        }

        [Fact]
        public async Task CancellationMarksToolAndRootAndDoesNotMakeAnotherModelCall()
        {
            using (var recording = new Recording(true))
            using (var fixture = new Fixture(recording.Tracing))
            {
                fixture.Handler.Enqueue(ToolTurn(Accounts, "cancelled"));
                fixture.Evidence.Read = (name, args, token) => throw new OperationCanceledException("PRIVATE cancellation");
                await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.AssessAsync());
                Assert.Single(fixture.Handler.Requests);
                var failed = recording.Activities.Where(a => a.Status == ActivityStatusCode.Error).ToArray();
                Assert.Equal(2, failed.Length);
                Assert.All(failed, a => Assert.Equal("cancelled_or_timeout", a.GetTagItem("error.type")));
                Assert.DoesNotContain("PRIVATE", AllTags(recording));
            }
        }

        [Theory]
        [InlineData("alice@example.test", "alice@example.test")]
        [InlineData("Bearer PRIVATE_TOKEN", "PRIVATE_TOKEN")]
        [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjMifQ.signature", "eyJhbGci")]
        [InlineData("api-key=PRIVATE_KEY", "PRIVATE_KEY")]
        [InlineData("password='PRIVATE password with spaces'", "PRIVATE password")]
        [InlineData("Server=PRIVATE_SERVER;Database=PRIVATE_DB;User Id=PRIVATE_USER;Password=PRIVATE_PASSWORD", "PRIVATE_")]
        [InlineData("https://user:PRIVATE_PASSWORD@example.test/path?token=PRIVATE_QUERY#PRIVATE_FRAGMENT", "PRIVATE_")]
        [InlineData("sk-PRIVATE_MODEL_KEY", "PRIVATE_MODEL_KEY")]
        [InlineData("requestKey=987654 ownerKeys=[123,456]", "987654")]
        public void SanitizesFreeTextSecretsEmailsAndUrls(string text, string secret)
        {
            var safe = NamespaceReservationTraceContent.Text(text);
            Assert.DoesNotContain(secret, safe);
            Assert.Contains("REDACTED", safe);
        }

        [Theory]
        [InlineData("get_namespace_reservations", "completenessScope", "sql_fact_read_only_not_all_policy_checks; aggregates_independent_of_sample_truncation_and_optional_publication_history")]
        [InlineData("get_namespace_reservations", "matchingSemantics", "raw_prefix_including_non_dotted_prefixes")]
        [InlineData(Accounts, "eligibilityScope", "account_state_and_submitter_authority_only")]
        [InlineData("get_namespace_package_usage", "coverage", "exact_base_or_base_followed_by_dot")]
        [InlineData("get_namespace_package_usage", "packageCountSemantics", "current_persisted_package_registrations_not_historical_existence")]
        [InlineData("get_namespace_package_usage", "thirdPartyOnlyCountSemantics", "per_package_id_with_owners_but_no_requested_owner; coowned_ids_are_separate; unlisted_requires_available_versions_and_no_listed_available_versions_or_uncertain_statuses; uncertain_means_no_available_versions_or_any_deleted_validating_failed_or_unknown_status; listed_and_uncertain_may_overlap")]
        [InlineData("get_namespace_package_usage", "recentStoredPublicationAndListedSemantics", "at_least_one_currently_listed_available_version_and_at_least_one_available_version_with_stored_Published_in_cutoff_to_assessment_window; these_may_be_different_versions; not_verified_publication_activity_or_first_publication")]
        public void PreservesExactApplicationOwnedEvidenceMetadata(string tool, string field, string value)
        {
            var original = new JObject { [field] = value };
            var safe = NamespaceReservationTraceContent.ToolResult(tool, original);
            Assert.Equal(value, (string)safe[field]);
            Assert.Equal(value, (string)original[field]);

            // Exempt only the exact known value, not arbitrary content in a trusted-looking field.
            original[field] = value + " password=PRIVATE_SECRET alice@example.test";
            safe = NamespaceReservationTraceContent.ToolResult(tool, original);
            Assert.Contains("REDACTED", (string)safe[field]);
            Assert.DoesNotContain("PRIVATE_SECRET", (string)safe[field]);
            Assert.DoesNotContain("alice@example.test", (string)safe[field]);
            Assert.Contains("REDACTED", NamespaceReservationTraceContent.Text(value));
        }

        [Theory]
        [InlineData("security_onboarding_not_established_by_subscription")]
        [InlineData("account_state_or_submitter_authority_not_satisfied")]
        [InlineData("previously_observed_package_missing")]
        [InlineData("package_id_not_in_session_evidence")]
        public void PreservesExactEvidenceCodesButNotArbitraryTokens(string code)
        {
            var unknown = new string('x', 40);
            var safe = NamespaceReservationTraceContent.ToolResult(Accounts, new JObject
            {
                ["error"] = code,
                ["blockers"] = new JArray(code, unknown, "Bearer PRIVATE_TOKEN"),
                ["submitter"] = new JObject { ["username"] = code }
            });
            Assert.Equal(code, (string)safe["error"]);
            Assert.Equal(code, (string)safe["blockers"][0]);
            Assert.Contains("REDACTED", (string)safe["blockers"][1]);
            Assert.DoesNotContain("PRIVATE_TOKEN", safe.ToString());
            Assert.Contains("REDACTED", (string)safe["submitter"]["username"]);
        }

        [Fact]
        public void ToolProjectionDropsUnknownPropertiesNestedKeysAndNumericIdentifiers()
        {
            var original = new JObject
            {
                ["complete"] = true, ["eligible"] = true, ["requestKey"] = 987654,
                ["submitter"] = new JObject
                {
                    ["username"] = "alice@example.test", ["key"] = 123456,
                    ["EmailAddress"] = "private@example.test", ["headers"] = new JObject { ["Authorization"] = "Bearer PRIVATE" },
                    ["accountType"] = "user", ["confirmed"] = true,
                    ["securityOnboardingStatus"] = new JObject { ["connectionString"] = "PRIVATE" }
                },
                ["owners"] = new JArray(new JObject { ["username"] = 456789, ["adminMembership"] = true, ["ownerKey"] = 456789 }),
                ["encrypted_content"] = "PRIVATE"
            };
            var before = original.ToString();
            var safe = NamespaceReservationTraceContent.ToolResult(Accounts, original).ToString();
            Assert.Equal(before, original.ToString());
            Assert.Contains("REDACTED_EMAIL", safe);
            Assert.Contains("adminMembership", safe);
            foreach (var value in new[] { "987654", "123456", "456789", "PRIVATE", "EmailAddress", "headers", "encrypted_content", "connectionString" })
            {
                Assert.DoesNotContain(value, safe);
            }
        }

        [Theory]
        [InlineData("get_namespace_reservations", "samples", "namespaceValue")]
        [InlineData("get_namespace_package_usage", "samples", "packageId")]
        [InlineData("get_package_details", "packages", "packageId")]
        public void ProjectsOnlyKnownSqlFieldsAndRedactsPackageAndNamespaceStrings(string name, string rows, string idField)
        {
            var safe = NamespaceReservationTraceContent.ToolResult(name, new JObject
            {
                ["complete"] = true,
                [rows] = new JArray(new JObject { [idField] = "alice@example.test", ["ownerCount"] = 2, ["key"] = 999999, ["password"] = "PRIVATE" }),
                ["counts"] = new JObject { ["packageCount"] = 4, ["overlapCount"] = 1, ["requestKey"] = 999999 }
            }).ToString();
            Assert.Contains("REDACTED_EMAIL", safe);
            Assert.Contains("ownerCount", safe);
            Assert.DoesNotContain("999999", safe);
            Assert.DoesNotContain("PRIVATE", safe);
        }

        [Theory]
        [InlineData("Bearer PRIVATE")]
        [InlineData("alice@example.test")]
        [InlineData("https://example.test/?secret=PRIVATE")]
        public void UnsafeModelAndCallIdentifiersAreNotRecorded(string id)
        {
            using (var recording = new Recording(true))
            {
                using (var scope = recording.Tracing.StartTool("PRIVATE_invented_tool", id, 1))
                {
                    scope.Complete();
                }

                var activity = Assert.Single(recording.Activities);
                Assert.Equal("execute_tool unknown", activity.OperationName);
                Assert.Equal("unknown", activity.GetTagItem("gen_ai.tool.call.id"));
                Assert.DoesNotContain("PRIVATE", AllTags(recording));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void OversizedContentUsesValidJsonPlaceholderAndExplicitTruncation(bool unicode)
        {
            using (var recording = new Recording(true))
            {
                var input = CreateInput();
                // Short words exercise the size limit after sanitization, rather than token
                // redaction or regex timeouts on a single oversized unbroken string.
                input.Justification = string.Join(" ", Enumerable.Repeat(unicode ? "\u4e2d\u6587" : "example", 4000));
                using (var scope = recording.Tracing.StartModel("test", 1, 1024, "auto"))
                {
                    scope.Input(input);
                    scope.Complete();
                }

                var activity = Assert.Single(recording.Activities);
                var json = (string)activity.GetTagItem("gen_ai.input.messages");
                Assert.NotNull(JArray.Parse(json));
                Assert.True(json.Length < 8192);
                Assert.True(System.Text.Encoding.UTF8.GetByteCount(json) < 8192);
                Assert.Equal(true, activity.GetTagItem("namespace_reservation.content.truncated"));
                Assert.Contains("truncated", json);
            }
        }

        [Fact]
        public void SanitizationFailureOmitsAttributeWithoutChangingScope()
        {
            using (var recording = new Recording(true))
            {
                var input = CreateInput();
                input.Justification = new string('x', 64001);
                using (var scope = recording.Tracing.StartModel("test", 1, 1024, "none"))
                {
                    var current = Activity.Current;
                    scope.Input(input);
                    Assert.Same(current, Activity.Current);
                    scope.Complete();
                }

                Assert.Null(Assert.Single(recording.Activities).GetTagItem("gen_ai.input.messages"));
            }
        }

        [Fact]
        public void DisabledTracerDoesNotStartActivitiesOrDisturbAmbientActivity()
        {
            using (var parent = new Activity("ambient").Start())
            using (var scope = NamespaceReservationTracing.Disabled.StartAssessment())
            {
                Assert.Same(parent, Activity.Current);
                scope.Input(CreateInput());
                scope.Error(new Exception("PRIVATE"));
                scope.Complete();
                Assert.Same(parent, Activity.Current);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FailedAssessmentSaveReportsStageAndSqlNumbersWithoutExceptionMessages(bool captureContent)
        {
            using (var recording = new Recording(captureContent))
            {
                var sqlException = Services.Authentication.FederatedCredentialServiceFacts.GetSqlException(547);
                var exception = new DbUpdateException("PRIVATE update details", new DataException("PRIVATE connection details", sqlException));
                using (var scope = recording.Tracing.StartAssessment(assessmentOnly: false))
                {
                    scope.Persistence(initial: true, succeeded: true);
                    scope.Persistence(initial: false, succeeded: false, exception: exception);
                }

                var root = Assert.Single(recording.Activities);
                Assert.Equal(ActivityStatusCode.Error, root.Status);
                Assert.Equal("Namespace reservation assessment_persistence failed.", root.StatusDescription);
                Assert.Equal(root.StatusDescription, root.GetTagItem("error.message"));
                Assert.Equal("assessment_persistence", root.GetTagItem("namespace_reservation.failure.stage"));
                Assert.Equal(typeof(DbUpdateException).FullName, root.GetTagItem("exception.type"));
                Assert.Equal(547, root.GetTagItem("namespace_reservation.sql.error_number"));
                Assert.Equal(2, root.GetTagItem("namespace_reservation.sql.error_state"));
                Assert.Equal(true, root.GetTagItem("namespace_reservation.persistence.pending.succeeded"));
                Assert.Equal(false, root.GetTagItem("namespace_reservation.persistence.assessment.succeeded"));
                Assert.Equal(false, root.GetTagItem("namespace_reservation.assessment_only"));
                Assert.Equal(false, root.GetTagItem("namespace_reservation.operation.complete"));
                Assert.Null(root.GetTagItem("exception.message"));
                Assert.Null(root.GetTagItem("exception.stacktrace"));
                Assert.DoesNotContain("PRIVATE", AllTags(recording));
                Assert.DoesNotContain(sqlException.Server, AllTags(recording));
            }
        }

        [Theory]
        [InlineData("assessment", "assessment")]
        [InlineData("reservation", "reservation")]
        [InlineData("PRIVATE unexpected stage", "operation")]
        public void FailureStagesHaveSafeDescriptionsWithoutCustomerOrExceptionContent(string stage, string expectedStage)
        {
            using (var recording = new Recording(true))
            {
                using (var scope = recording.Tracing.StartAssessment(assessmentOnly: false))
                {
                    scope.Error(new InvalidOperationException("PRIVATE request contents"), stage);
                }

                var root = Assert.Single(recording.Activities);
                Assert.Equal(expectedStage, root.GetTagItem("namespace_reservation.failure.stage"));
                Assert.Equal("Namespace reservation " + expectedStage + " failed.", root.StatusDescription);
                Assert.Equal(typeof(InvalidOperationException).FullName, root.GetTagItem("exception.type"));
                Assert.DoesNotContain("PRIVATE", AllTags(recording));
            }
        }

        [Theory]
        [InlineData("sample")]
        [InlineData("start")]
        [InlineData("stop")]
        public async Task ThrowingListenersDoNotChangeAssessmentOrStrandCurrent(string phase)
        {
            var source = new ActivitySource("NamespaceTracingFacts." + Guid.NewGuid().ToString("N"));
            using (var listener = new ActivityListener
            {
                ShouldListenTo = candidate => candidate == source,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => phase == "sample"
                    ? throw new InvalidOperationException("PRIVATE sample") : ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => { if (phase == "start") { throw new InvalidOperationException("PRIVATE start"); } },
                ActivityStopped = activity => { if (phase == "stop") { throw new InvalidOperationException("PRIVATE stop"); } }
            })
            using (var tracing = new NamespaceReservationTracing(source, true))
            using (var fixture = new Fixture(tracing))
            using (var parent = new Activity("ambient").SetIdFormat(ActivityIdFormat.W3C).Start())
            {
                ActivitySource.AddActivityListener(listener);
                fixture.Handler.Enqueue(ToolTurn(Accounts, "call_one"));
                fixture.Handler.Enqueue(Final(Decision("Rejected", new[] { Accounts })));
                Assert.Equal("Rejected", (await fixture.AssessAsync()).Status);
                Assert.Same(parent, Activity.Current);
                Assert.Single(fixture.Evidence.Calls);
                Assert.Equal(2, fixture.Handler.Requests.Count);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ThrowingProcessorsDoNotChangeWorkflow(bool onStart)
        {
            var source = new ActivitySource("NamespaceTracingFacts." + Guid.NewGuid().ToString("N"));
            var provider = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateEmpty())
                .SetSampler(new AlwaysOnSampler())
                .AddSource(source.Name)
                .AddProcessor(new ThrowingProcessor(onStart))
                .Build();
            using (var tracing = new NamespaceReservationTracing(source, true, provider))
            using (var fixture = new Fixture(tracing))
            using (var parent = new Activity("ambient").SetIdFormat(ActivityIdFormat.W3C).Start())
            {
                fixture.Handler.Enqueue(Final(Decision("Rejected")));
                Assert.Equal("Rejected", (await fixture.AssessAsync()).Status);
                Assert.Same(parent, Activity.Current);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task SubmissionRootIncludesInitialAndFinalPersistenceWithoutDatabaseKeys(int failure)
        {
            using (var recording = new Recording(true))
            using (var fixture = new Fixture(recording.Tracing))
            {
                fixture.Handler.Enqueue(Final(Decision("Rejected")));
                var repository = new Mock<IEntityRepository<NamespaceReservationRequest>>();
                NamespaceReservationRequest inserted = null;
                repository.Setup(r => r.InsertOnCommit(It.IsAny<NamespaceReservationRequest>()))
                    .Callback<NamespaceReservationRequest>(request => inserted = request);
                var commits = 0;
                repository.Setup(r => r.CommitChangesAsync()).Returns(() =>
                {
                    commits++;
                    if (commits == failure || (failure == 3 && commits == 2))
                    {
                        return Task.FromException(failure == 3 ? (Exception)new InvalidOperationException("PRIVATE unexpected failure") : new DataException("PRIVATE database failure"));
                    }

                    if (commits == 1)
                    {
                        inserted.Key = 123456;
                    }

                    return Task.CompletedTask;
                });
                var user = new User("Alice") { Key = 987654, EmailAddress = "private@example.test" };
                var users = new Mock<IUserService>();
                users.Setup(u => u.FindByUsername("Alice", false)).Returns(user);
                var evidence = new Mock<INamespaceReservationEvidenceFactory>();
                evidence.Setup(e => e.Create(It.IsAny<int>(), It.IsAny<int[]>(), It.IsAny<string>())).Returns(fixture.Evidence);
                var reservations = new Mock<IReservedNamespaceService>(MockBehavior.Strict);
                var service = new NamespaceReservationRequestService(repository.Object, users.Object,
                    new Lazy<INamespaceReservationFoundryClient>(() => fixture.Client), Mock.Of<ITelemetryService>(), evidence.Object, reservations.Object, recording.Tracing);
                var input = new NamespaceReservationRequestInput { Namespace = "Example.Product", Owner = "Alice", Justification = "Our product." };

                if (failure == 1)
                {
                    await Assert.ThrowsAsync<DataException>(() => service.SubmitAsync(user, input));
                }
                else if (failure == 3)
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitAsync(user, input));
                }
                else
                {
                    var result = await service.SubmitAsync(user, input);
                    Assert.Empty(result.Errors);
                    Assert.Contains("No namespace has been reserved", result.Message);
                    if (failure == 2)
                    {
                        Assert.Contains("could not confirm", result.Message);
                    }
                }

                var root = Assert.Single(recording.Activities.Where(a => a.OperationName.StartsWith("invoke_agent ", StringComparison.Ordinal)));
                var events = root.Events.Where(e => e.Name.EndsWith("persistence", StringComparison.Ordinal)).ToArray();
                Assert.Equal(false, root.GetTagItem("namespace_reservation.assessment_only"));
                Assert.Equal(failure != 1, root.GetTagItem("namespace_reservation.persistence.pending.succeeded"));
                if (failure > 0)
                {
                    var stage = failure == 1 ? "pending_persistence" : "assessment_persistence";
                    Assert.Equal(stage, root.GetTagItem("namespace_reservation.failure.stage"));
                    Assert.Equal("Namespace reservation " + stage + " failed.", root.StatusDescription);
                    Assert.Equal(failure == 3 ? typeof(InvalidOperationException).FullName : typeof(DataException).FullName,
                        root.GetTagItem("exception.type"));
                }

                Assert.Equal(failure == 1 ? 1 : 2, events.Length);
                Assert.Equal("pending_persistence", events[0].Name);
                Assert.Equal(failure != 1, events[0].Tags.Single().Value);
                if (failure != 1)
                {
                    Assert.Equal("final_persistence", events[1].Name);
                    Assert.Equal(failure == 0, events[1].Tags.Single().Value);
                }

                Assert.Equal(failure == 0 ? ActivityStatusCode.Unset : ActivityStatusCode.Error, root.Status);
                Assert.Equal(failure == 1 ? 0 : 1, fixture.Handler.Requests.Count);
                Assert.All(recording.Activities.Where(a => a != root), a => Assert.Equal(root.SpanId, a.ParentSpanId));
                reservations.Verify(value => value.ReserveNamespaceForRequestAsync(
                    It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>()), Times.Never);
                reservations.VerifyNoOtherCalls();
                Assert.DoesNotContain("123456", AllTags(recording));
                Assert.DoesNotContain("987654", AllTags(recording));
                Assert.DoesNotContain("private@example.test", AllTags(recording));
                Assert.DoesNotContain("PRIVATE", AllTags(recording));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SubmissionAllocationFailureIsAttributedToReservationWithoutChangingSavedDecisionOrLeakingPrivateContent(bool captureContent)
        {
            using (var recording = new Recording(captureContent))
            {
                var repository = new Mock<IEntityRepository<NamespaceReservationRequest>>(MockBehavior.Strict);
                NamespaceReservationRequest inserted = null;
                var saved = new List<NamespaceReservationRequest>();
                repository.Setup(r => r.InsertOnCommit(It.IsAny<NamespaceReservationRequest>()))
                    .Callback<NamespaceReservationRequest>(request => inserted = request);
                repository.Setup(r => r.CommitChangesAsync()).Returns(() =>
                {
                    if (saved.Count == 0)
                    {
                        inserted.Key = 123456;
                    }

                    saved.Add(new NamespaceReservationRequest
                    {
                        Key = inserted.Key,
                        Status = inserted.Status,
                        Reason = inserted.Reason,
                        CompletedTimestamp = inserted.CompletedTimestamp
                    });
                    return Task.CompletedTask;
                });
                var user = new User("PRIVATE_USER") { Key = 987654, EmailAddress = "private@example.test" };
                var users = new Mock<IUserService>(MockBehavior.Strict);
                users.Setup(service => service.FindByUsername(user.Username, false)).Returns(user);
                var evidenceSession = new Mock<INamespaceReservationEvidenceSession>(MockBehavior.Strict);
                var evidence = new Mock<INamespaceReservationEvidenceFactory>(MockBehavior.Strict);
                evidence.Setup(factory => factory.Create(user.Key, It.Is<int[]>(keys => keys.SequenceEqual(new[] { user.Key })), "PRIVATE_NAMESPACE"))
                    .Returns(evidenceSession.Object);
                var assessment = new NamespaceReservationAssessment("Approved", "criteria_met", "Criteria met.",
                    "PRIVATE rationale", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), 13, 7);
                var foundry = new Mock<INamespaceReservationFoundryClient>(MockBehavior.Strict);
                foundry.SetupGet(client => client.IsEnabled).Returns(true);
                foundry.Setup(client => client.AssessAsync(It.IsAny<NamespaceReservationAssessmentInput>(), evidenceSession.Object, CancellationToken.None))
                    .ReturnsAsync(assessment);
                var exception = new InvalidOperationException("PRIVATE allocation/audit token=secret", new Exception("PRIVATE inner exception"));
                var reservations = new Mock<IReservedNamespaceService>(MockBehavior.Strict);
                reservations.Setup(service => service.ReserveNamespaceForRequestAsync(
                    123456, "PRIVATE_NAMESPACE", user.Key, It.Is<IReadOnlyCollection<int>>(keys => keys.SequenceEqual(new[] { user.Key }))))
                    .Callback(() =>
                    {
                        Assert.Equal(2, saved.Count);
                        Assert.Equal("Approved", saved[1].Status);
                        Assert.NotNull(saved[1].CompletedTimestamp);
                    })
                    .ThrowsAsync(exception);
                Exception loggedException = null;
                var properties = new Dictionary<string, string>();
                var telemetry = new Mock<ITelemetryService>(MockBehavior.Strict);
                telemetry.Setup(service => service.TrackException(It.IsAny<Exception>(), It.IsAny<Action<Dictionary<string, string>>>()))
                    .Callback<Exception, Action<Dictionary<string, string>>>((error, addProperties) =>
                    {
                        loggedException = error;
                        addProperties(properties);
                    });
                var target = new NamespaceReservationRequestService(repository.Object, users.Object,
                    new Lazy<INamespaceReservationFoundryClient>(() => foundry.Object), telemetry.Object, evidence.Object, reservations.Object, recording.Tracing);

                var result = await target.SubmitAsync(user, new NamespaceReservationRequestInput
                {
                    Namespace = "PRIVATE_NAMESPACE",
                    Owner = user.Username,
                    Justification = "PRIVATE justification"
                });

                Assert.Empty(result.Errors);
                Assert.Equal("Your request was approved, but automatic namespace reservation could not be confirmed. Check the table or contact support before retrying.", result.Message);
                Assert.Equal(2, saved.Count);
                Assert.Equal("Pending", saved[0].Status);
                Assert.Null(saved[0].CompletedTimestamp);
                Assert.Equal("Approved", inserted.Status);
                Assert.Equal(assessment.Reason, inserted.Reason);
                Assert.Equal(saved[1].Reason, inserted.Reason);
                Assert.NotNull(saved[1].CompletedTimestamp);
                Assert.Equal(saved[1].CompletedTimestamp, inserted.CompletedTimestamp);
                var root = Assert.Single(recording.Activities);
                Assert.Equal("invoke_agent namespace-reservation-assessor", root.OperationName);
                Assert.Equal(ActivityStatusCode.Error, root.Status);
                Assert.Equal("reservation", root.GetTagItem("namespace_reservation.failure.stage"));
                Assert.Equal("Namespace reservation reservation failed.", root.StatusDescription);
                Assert.Equal(root.StatusDescription, root.GetTagItem("error.message"));
                Assert.Equal(typeof(InvalidOperationException).FullName, root.GetTagItem("exception.type"));
                Assert.Equal(false, root.GetTagItem("namespace_reservation.assessment_only"));
                Assert.Equal(false, root.GetTagItem("namespace_reservation.operation.complete"));
                Assert.Equal(true, root.GetTagItem("namespace_reservation.persistence.pending.succeeded"));
                Assert.Equal(true, root.GetTagItem("namespace_reservation.persistence.assessment.succeeded"));
                var persistence = root.Events.Where(e => e.Name.EndsWith("persistence", StringComparison.Ordinal)).ToArray();
                Assert.Equal(new[] { "pending_persistence", "final_persistence" }, persistence.Select(e => e.Name));
                Assert.All(persistence, e => Assert.Equal(true, e.Tags.Single().Value));
                Assert.Null(root.GetTagItem("exception.message"));
                Assert.Null(root.GetTagItem("exception.stacktrace"));
                var safeException = Assert.IsType<InvalidOperationException>(loggedException);
                Assert.NotSame(exception, safeException);
                Assert.Null(safeException.InnerException);
                Assert.Equal("Namespace reservation assessment processing failed.", safeException.Message);
                Assert.Equal(2, properties.Count);
                Assert.Equal("ReserveApprovedNamespaceRequest", properties["Operation"]);
                Assert.Equal(nameof(InvalidOperationException), properties["ExceptionType"]);
                var traceText = AllTags(recording) + root.StatusDescription
                    + string.Join("\n", root.Events.Select(e => e.Name + string.Join(";", e.Tags.Select(tag => tag.Key + "=" + tag.Value))))
                    + safeException + string.Join(";", properties.Select(property => property.Key + "=" + property.Value));
                foreach (var privateValue in new[] { "PRIVATE", "123456", "987654", "private@example.test", "token=secret" })
                {
                    Assert.DoesNotContain(privateValue, traceText);
                    Assert.DoesNotContain(privateValue, result.Message);
                }

                AssertNoContent(recording);
                repository.Verify(r => r.InsertOnCommit(inserted), Times.Once);
                repository.Verify(r => r.CommitChangesAsync(), Times.Exactly(2));
                repository.VerifyNoOtherCalls();
                reservations.Verify(value => value.ReserveNamespaceForRequestAsync(
                    123456, "PRIVATE_NAMESPACE", user.Key, It.Is<IReadOnlyCollection<int>>(keys => keys.SequenceEqual(new[] { user.Key }))), Times.Once);
                reservations.VerifyNoOtherCalls();
                foundry.VerifyGet(client => client.IsEnabled, Times.Once);
                foundry.Verify(client => client.AssessAsync(It.IsAny<NamespaceReservationAssessmentInput>(), evidenceSession.Object, CancellationToken.None), Times.Once);
                foundry.VerifyNoOtherCalls();
                telemetry.Verify(value => value.TrackException(It.IsAny<Exception>(), It.IsAny<Action<Dictionary<string, string>>>()), Times.Once);
                telemetry.VerifyNoOtherCalls();
            }
        }

        [Fact]
        public async Task ValidationRejectionContainsNoCustomerValuesOrModelCalls()
        {
            using (var recording = new Recording(true))
            {
                var repository = new Mock<IEntityRepository<NamespaceReservationRequest>>(MockBehavior.Strict);
                var reservations = new Mock<IReservedNamespaceService>(MockBehavior.Strict);
                var service = new NamespaceReservationRequestService(repository.Object, Mock.Of<IUserService>(),
                    new Lazy<INamespaceReservationFoundryClient>(() => throw new Exception("Must not resolve")),
                    Mock.Of<ITelemetryService>(), Mock.Of<INamespaceReservationEvidenceFactory>(), reservations.Object, recording.Tracing);
                var result = await service.SubmitAsync(new User("PRIVATE_USER"), new NamespaceReservationRequestInput { Namespace = "PRIVATE*" });
                Assert.NotEmpty(result.Errors);
                var span = Assert.Single(recording.Activities);
                Assert.Equal("rejected", span.GetTagItem("namespace_reservation.validation"));
                Assert.DoesNotContain("PRIVATE", AllTags(recording));
                AssertNoContent(recording);
                reservations.Verify(value => value.ReserveNamespaceForRequestAsync(
                    It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>()), Times.Never);
                reservations.VerifyNoOtherCalls();
            }
        }

        [Fact]
        public void DisposalFailureIsSwallowedAndAmbientActivityIsPreserved()
        {
            using (var parent = new Activity("ambient").Start())
            {
                var tracing = new NamespaceReservationTracing(new ActivitySource("NamespaceTracingFacts." + Guid.NewGuid().ToString("N")), false,
                    new Disposable { Throw = true });
                tracing.Dispose();
                Assert.Same(parent, Activity.Current);
            }
        }

        private static AppConfiguration Configuration()
        {
            return new AppConfiguration
            {
                Environment = ServicesConstants.DevelopmentEnvironment,
                NamespaceReservationTracingEnabled = true,
                NamespaceReservationTracingCaptureContent = true,
                NamespaceReservationTracingProjectEndpoint = ProjectEndpoint
            };
        }

        private static string SyntheticConnectionString()
        {
            // Random test identifier, never sent to an exporter or service.
            return "InstrumentationKey=" + Guid.NewGuid();
        }

        private static string AllTags(Recording recording)
        {
            return string.Join("\n", recording.Activities.SelectMany(a => a.TagObjects).Select(tag => tag.Key + "=" + tag.Value));
        }

        private static void AssertNoContent(Recording recording)
        {
            Assert.All(recording.Activities, activity =>
            {
                Assert.Null(activity.GetTagItem("gen_ai.input.messages"));
                Assert.Null(activity.GetTagItem("gen_ai.output.messages"));
                Assert.Null(activity.GetTagItem("gen_ai.tool.call.arguments"));
                Assert.Null(activity.GetTagItem("gen_ai.tool.call.result"));
            });
        }

        private sealed class Recording : IDisposable
        {
            private readonly ActivityListener _listener;
            public NamespaceReservationTracing Tracing { get; }
            public List<Activity> Activities { get; } = new List<Activity>();

            public Recording(bool captureContent, bool useSdk = false)
            {
                var source = new ActivitySource("NamespaceTracingFacts." + Guid.NewGuid().ToString("N"));
                if (useSdk)
                {
                    // Exercise production sampling without constructing any network exporter.
                    var provider = Sdk.CreateTracerProviderBuilder()
                        .SetResourceBuilder(ResourceBuilder.CreateEmpty())
                        .SetSampler(new AlwaysOnSampler())
                        .AddSource(source.Name)
                        .AddProcessor(new RecordingProcessor(Activities))
                        .Build();
                    Tracing = new NamespaceReservationTracing(source, captureContent, provider);
                    return;
                }

                _listener = new ActivityListener
                {
                    ShouldListenTo = candidate => candidate == source,
                    Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                    SampleUsingParentId = (ref ActivityCreationOptions<string> options) => ActivitySamplingResult.AllDataAndRecorded,
                    ActivityStopped = activity => Activities.Add(activity)
                };
                ActivitySource.AddActivityListener(_listener);
                Tracing = new NamespaceReservationTracing(source, captureContent);
            }

            public void Dispose()
            {
                Tracing.Dispose();
                _listener?.Dispose();
            }
        }

        private sealed class RecordingProcessor : BaseProcessor<Activity>
        {
            private readonly List<Activity> _activities;

            public RecordingProcessor(List<Activity> activities)
            {
                _activities = activities;
            }

            public override void OnEnd(Activity data)
            {
                _activities.Add(data);
            }
        }

        private sealed class RecordingTelemetryHandler : HttpMessageHandler
        {
            public List<JObject> Envelopes { get; } = new List<JObject>();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                // Every exporter request ends here. No network transport or credentials are used.
                var bytes = await request.Content.ReadAsByteArrayAsync();
                string json;
                if (bytes.Length >= 2 && bytes[0] == 0x1f && bytes[1] == 0x8b)
                {
                    using (var input = new MemoryStream(bytes))
                    using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                    using (var reader = new StreamReader(gzip, Encoding.UTF8))
                    {
                        json = await reader.ReadToEndAsync();
                    }
                }
                else
                {
                    json = Encoding.UTF8.GetString(bytes);
                }

                if (json.TrimStart().StartsWith("[", StringComparison.Ordinal))
                {
                    Envelopes.AddRange(JArray.Parse(json).OfType<JObject>());
                }
                else
                {
                    Envelopes.AddRange(json.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(JObject.Parse));
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"itemsReceived\":1,\"itemsAccepted\":1,\"errors\":[]}")
                };
            }
        }

        private sealed class Disposable : IDisposable
        {
            public bool Disposed { get; private set; }
            public bool Throw { get; set; }

            public void Dispose()
            {
                Disposed = true;
                if (Throw)
                {
                    throw new InvalidOperationException("PRIVATE disposal failure");
                }
            }
        }

        private sealed class ThrowingProcessor : BaseProcessor<Activity>
        {
            private readonly bool _onStart;

            public ThrowingProcessor(bool onStart)
            {
                _onStart = onStart;
            }

            public override void OnStart(Activity data)
            {
                if (_onStart)
                {
                    throw new InvalidOperationException("PRIVATE processor start");
                }
            }

            public override void OnEnd(Activity data)
            {
                if (!_onStart)
                {
                    throw new InvalidOperationException("PRIVATE processor end");
                }
            }
        }
    }
}