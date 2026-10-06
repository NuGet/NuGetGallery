// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Azure.Monitor.OpenTelemetry.Exporter;
using Newtonsoft.Json.Linq;
using NuGetGallery.Configuration;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace NuGetGallery
{
    /// <summary>
    /// Local POC tracing only. Owns an isolated provider and only this ActivitySource. No automatic
    /// instrumentation, cloud metadata, audit writes, global switches, or Gallery telemetry changes.
    /// All mutable assessment state belongs to operation scopes, never this singleton.
    /// </summary>
    public sealed class NamespaceReservationTracing : IDisposable
    {
        internal const string SourceName = "NuGetGallery.NamespaceReservation.Poc";
        internal const string ConnectionStringVariable = "NUGET_NAMESPACE_POC_APPLICATIONINSIGHTS_CONNECTION_STRING";
        private const string AgentOperation = "invoke_agent namespace-reservation-assessor";
        private readonly ActivitySource _source;
        private readonly IDisposable _provider;
        private readonly bool _captureContent;
        private readonly string _projectEndpoint;

        public static NamespaceReservationTracing Disabled { get; } = new NamespaceReservationTracing("disabled");
        public string ConfigurationStatus { get; }
        public bool IsEnabled => _source != null;

        private NamespaceReservationTracing(string status)
        {
            ConfigurationStatus = status;
        }

        // Test seam: a unique source and recording listener/processor, with no exporter or network.
        internal NamespaceReservationTracing(ActivitySource source, bool captureContent, IDisposable provider = null)
        {
            _source = source;
            _captureContent = captureContent;
            _provider = provider;
            ConfigurationStatus = "enabled";
        }

        private NamespaceReservationTracing(ActivitySource source, bool captureContent, IDisposable provider, string endpoint)
            : this(source, captureContent, provider)
        {
            _projectEndpoint = endpoint;
        }

        public static NamespaceReservationTracing Create(IAppConfiguration configuration)
        {
            return Create(configuration, Environment.GetEnvironmentVariable, CreateProvider);
        }

        internal static NamespaceReservationTracing Create(
            IAppConfiguration configuration,
            Func<string, EnvironmentVariableTarget, string> readEnvironment,
            Func<string, string, IDisposable> createProvider)
        {
            ActivitySource source = null;
            IDisposable provider = null;
            try
            {
                if (configuration == null || !configuration.NamespaceReservationTracingEnabled)
                {
                    return Disabled;
                }

                if (configuration.Environment != ServicesConstants.DevelopmentEnvironment)
                {
                    return new NamespaceReservationTracing("not_development");
                }

                var endpoint = configuration.NamespaceReservationTracingProjectEndpoint;
                if (!IsProjectEndpoint(endpoint))
                {
                    return new NamespaceReservationTracing("invalid_project_endpoint");
                }

                // Only this dedicated variable, in this order. Never read the general App Insights
                // variable, Gallery settings, machine environment, or credential/config files.
                var connectionString = readEnvironment(ConnectionStringVariable, EnvironmentVariableTarget.Process);
                if (string.IsNullOrWhiteSpace(connectionString) && Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    connectionString = readEnvironment(ConnectionStringVariable, EnvironmentVariableTarget.User);
                }

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    return new NamespaceReservationTracing("missing_dedicated_connection_string");
                }

                if (!IsConnectionString(connectionString))
                {
                    return new NamespaceReservationTracing("invalid_dedicated_connection_string");
                }

                var captureContent = configuration.NamespaceReservationTracingCaptureContent;
                source = new ActivitySource(SourceName);
                provider = createProvider(SourceName, connectionString);
                return new NamespaceReservationTracing(source, captureContent, provider, endpoint);
            }
            catch (Exception)
            {
                // Configuration, SDK initialization and diagnostic callbacks must not prevent saving.
                Safe(() => provider?.Dispose());
                Safe(() => source?.Dispose());
                return new NamespaceReservationTracing("initialization_failed");
            }
        }

        private static IDisposable CreateProvider(string sourceName, string connectionString)
        {
            return Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService(SourceName, serviceInstanceId: "local-poc"))
                .AddSource(sourceName)
                .AddAzureMonitorTraceExporter(options =>
                {
                    options.ConnectionString = connectionString;
                    // This exporter installs its own ApplicationInsightsSampler.
                    options.SamplingRatio = 1.0f;
                    options.DisableOfflineStorage = true;
                    options.Diagnostics.IsLoggingEnabled = false;
                    options.Diagnostics.IsLoggingContentEnabled = false;
                    options.Diagnostics.IsDistributedTracingEnabled = false;
                })
                .Build();
        }

        internal static bool IsProjectEndpoint(string value)
        {
            return value != null && value.Length <= 512
                && Regex.IsMatch(value, @"\Ahttps://[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,62})\.services\.ai\.azure\.com/api/projects/[a-zA-Z0-9][a-zA-Z0-9_-]{0,127}\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50))
                && Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort
                && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0;
        }

        private static bool IsConnectionString(string value)
        {
            if (value.Length > 4096)
            {
                return false;
            }

            var parsed = new DbConnectionStringBuilder { ConnectionString = value };
            if (!parsed.TryGetValue("InstrumentationKey", out var key)
                || !Guid.TryParse(key as string, out var id) || id == Guid.Empty)
            {
                return false;
            }

            var allowed = new[] { "InstrumentationKey", "IngestionEndpoint", "LiveEndpoint", "ApplicationId", "EndpointSuffix", "Location", "Authorization" };
            foreach (string name in parsed.Keys)
            {
                if (!allowed.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (name.EndsWith("Endpoint", StringComparison.OrdinalIgnoreCase)
                    && (!Uri.TryCreate(parsed[name] as string, UriKind.Absolute, out var endpoint)
                        || endpoint.Scheme != Uri.UriSchemeHttps || !endpoint.IsDefaultPort
                        || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0))
                {
                    return false;
                }
            }

            return true;
        }

        internal Scope StartAssessment(bool assessmentOnly = true)
        {
            if (!IsEnabled)
            {
                return Scope.Disabled;
            }

            // Reuse only our own assessment ancestor, not an unrelated ASP.NET/SDK activity.
            for (var parent = Activity.Current; parent != null; parent = parent.Parent)
            {
                if (parent.Source == _source && parent.OperationName == AgentOperation)
                {
                    return new Scope(parent, false, _captureContent, Activity.Current);
                }
            }

            var scope = Start(AgentOperation, ActivityKind.Internal, startRoot: true);
            scope.Update(activity =>
            {
                activity.SetTag("gen_ai.operation.name", "invoke_agent");
                activity.SetTag("gen_ai.agent.name", "namespace-reservation-assessor");
                activity.SetTag("gen_ai.provider.name", "azure.ai.openai");
                activity.SetTag("gen_ai.system", "az.ai.openai");
                activity.SetTag("namespace_reservation.assessment_only", assessmentOnly);
                // Only the configured project identifier, not a model request URL or connection string.
                activity.SetTag("gen_ai.project.endpoint", _projectEndpoint);
                var skill = NamespaceReservationAssessmentContract.Skill;
                var version = Regex.Match(skill, "(?m)^  version: \"([0-9]+\\.[0-9]+\\.[0-9]+)\"\\r?$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
                if (version.Success)
                {
                    activity.SetTag("namespace_reservation.skill.version", version.Groups[1].Value);
                }

                using (var hash = SHA256.Create())
                {
                    activity.SetTag("namespace_reservation.skill.sha256", BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(skill))).Replace("-", "").ToLowerInvariant());
                }
            });
            return scope;
        }

        internal Scope StartModel(string deployment, int turn, int maxTokens, string toolChoice, bool webSearchEnabled = false)
        {
            var safeDeployment = "unknown";
            Safe(() => safeDeployment = NamespaceReservationTraceContent.Identifier(deployment));
            var scope = Start("chat " + safeDeployment, ActivityKind.Client);
            scope.Update(activity =>
            {
                activity.SetTag("gen_ai.operation.name", "chat");
                activity.SetTag("gen_ai.provider.name", "azure.ai.openai");
                activity.SetTag("gen_ai.system", "az.ai.openai");
                activity.SetTag("gen_ai.request.model", safeDeployment);
                activity.SetTag("gen_ai.request.max_tokens", maxTokens);
                activity.SetTag("gen_ai.request.tool_choice", toolChoice == "auto" ? "auto" : "none");
                activity.SetTag("namespace_reservation.model.turn", turn);
                activity.SetTag("namespace_reservation.web_search.enabled", webSearchEnabled);
            });
            return scope;
        }

        internal Scope StartTool(string name, string callId, int attempt)
        {
            var safeName = NamespaceReservationTraceContent.ToolName(name);
            var scope = Start("execute_tool " + safeName, ActivityKind.Internal);
            scope.Update(activity =>
            {
                activity.SetTag("gen_ai.operation.name", "execute_tool");
                activity.SetTag("gen_ai.tool.name", safeName);
                activity.SetTag("gen_ai.tool.type", "function");
                activity.SetTag("gen_ai.tool.call.id", NamespaceReservationTraceContent.Identifier(callId));
                activity.SetTag("namespace_reservation.tool.attempt", attempt);
                activity.SetTag("namespace_reservation.tools.used", attempt);
            });
            return scope;
        }

        internal void ValidationRejected()
        {
            using (var scope = Start("namespace_reservation.validation", ActivityKind.Internal))
            {
                scope.Update(activity => activity.SetTag("namespace_reservation.validation", "rejected"));
                scope.Complete();
            }
        }

        private Scope Start(string name, ActivityKind kind, bool startRoot = false)
        {
            if (_source == null)
            {
                return Scope.Disabled;
            }

            var previous = Activity.Current;
            Activity activity = null;
            try
            {
                // Keep a reference before Start: a throwing listener can otherwise strand Current.
                // This destination exports only our source, not the ambient Gallery HTTP request.
                // An empty parent ID alone is insufficient with the exporter's sampler: during
                // creation it can become a default parent context and fall back to Activity.Current.
                // Clear the ambient activity for root creation AND Start, without changing globals.
                if (startRoot)
                {
                    Activity.Current = null;
                }

                activity = startRoot
                    ? _source.CreateActivity(name, kind, parentContext: default(ActivityContext), idFormat: ActivityIdFormat.W3C)
                    : _source.CreateActivity(name, kind);
                activity?.Start();
                if (activity == null)
                {
                    Restore(previous);
                    return Scope.Disabled;
                }

                return new Scope(activity, true, _captureContent, previous);
            }
            catch (Exception)
            {
                Safe(() => activity?.Dispose());
                Restore(previous);
                return Scope.Disabled;
            }
        }

        internal static void Safe(Action action)
        {
            var previous = Activity.Current;
            try
            {
                action();
            }
            catch (Exception)
            {
                // No fallback diagnostics: they could leak content or redirect telemetry.
            }
            finally
            {
                Restore(previous);
            }
        }

        private static void Restore(Activity activity)
        {
            try
            {
                Activity.Current = activity;
            }
            catch (Exception)
            {
                // CurrentChanged listeners can throw, even though the AsyncLocal is already set.
            }
        }

        public void Dispose()
        {
            Safe(() => _provider?.Dispose());
            Safe(() => _source?.Dispose());
        }

        internal sealed class Scope : IDisposable
        {
            internal static readonly Scope Disabled = new Scope(null, false, false, null);
            private readonly Activity _activity;
            private readonly bool _ownsActivity;
            private readonly bool _captureContent;
            private readonly Activity _previous;
            private bool _completed;
            private bool _disposed;
            private int _contentBytes;

            internal Scope(Activity activity, bool ownsActivity, bool captureContent, Activity previous)
            {
                _activity = activity;
                _ownsActivity = ownsActivity;
                _captureContent = captureContent;
                _previous = previous;
            }

            internal void Update(Action<Activity> update)
            {
                if (_activity != null)
                {
                    Safe(() => update(_activity));
                }
            }

            internal void Complete()
            {
                if (_activity == null)
                {
                    return;
                }

                _completed = true;
                Update(activity => activity.SetTag("namespace_reservation.operation.complete", activity.Status != ActivityStatusCode.Error));
            }

            internal void Error(Exception exception = null, string stage = "operation")
            {
                if (_activity == null)
                {
                    return;
                }

                _completed = true;
                Update(activity =>
                {
                    // Only application-owned descriptions and structural exception metadata. SQL
                    // messages, stack traces, server names and request values are never exported.
                    var safeStage = stage == "pending_persistence" || stage == "assessment_persistence"
                        || stage == "assessment" || stage == "reservation" ? stage : "operation";
                    var description = "Namespace reservation " + safeStage + " failed.";
                    activity.SetStatus(ActivityStatusCode.Error, description);
                    activity.SetTag("error.type", exception is OperationCanceledException ? "cancelled_or_timeout" : "operation_failed");
                    activity.SetTag("error.message", description);
                    activity.SetTag("namespace_reservation.failure.stage", safeStage);
                    activity.SetTag("exception.type", exception?.GetType().FullName);
                    var current = exception;
                    for (var depth = 0; current != null && depth < 8; current = current.InnerException, depth++)
                    {
                        if (current is SqlException sqlException)
                        {
                            activity.SetTag("namespace_reservation.sql.error_number", sqlException.Number);
                            activity.SetTag("namespace_reservation.sql.error_state", (int)sqlException.State);
                            break;
                        }
                    }

                    activity.SetTag("namespace_reservation.operation.complete", false);
                });
            }

            internal void Persistence(bool initial, bool succeeded, Exception exception = null)
            {
                Update(activity =>
                {
                    // Keep flags directly on the span: Foundry may display events separately.
                    activity.SetTag(initial ? "namespace_reservation.persistence.pending.succeeded"
                        : "namespace_reservation.persistence.assessment.succeeded", succeeded);
                    activity.AddEvent(new ActivityEvent(initial ? "pending_persistence" : "final_persistence", tags: new ActivityTagsCollection
                    {
                        { "namespace_reservation.persistence.succeeded", succeeded }
                    }));
                });
                if (!succeeded)
                {
                    Error(exception, initial ? "pending_persistence" : "assessment_persistence");
                }
            }

            internal void Decision(NamespaceReservationAssessment model, NamespaceReservationAssessment accepted, int toolsUsed)
            {
                Update(activity =>
                {
                    var recommendation = Status(model.Status);
                    var outcome = Status(accepted.Status);
                    activity.SetTag("namespace_reservation.model.recommendation", recommendation);
                    activity.SetTag("namespace_reservation.guard.accepted", outcome);
                    activity.SetTag("namespace_reservation.guard.overridden", model.Status != accepted.Status);
                    activity.SetTag("namespace_reservation.tools.used", toolsUsed);
                    activity.AddEvent(new ActivityEvent("model_recommendation", tags: new ActivityTagsCollection { { "status", recommendation } }));
                    activity.AddEvent(new ActivityEvent("guard_accepted_assessment", tags: new ActivityTagsCollection { { "status", outcome } }));
                });
                Complete();
            }

            private static string Status(string value)
            {
                return value == "Approved" || value == "Pending" || value == "Rejected" ? value : "unknown";
            }

            internal void HttpStatus(int status)
            {
                Update(activity => activity.SetTag("http.response.status_code", status));
            }

            internal void Response(JObject json)
            {
                Update(activity =>
                {
                    activity.SetTag("gen_ai.response.id", NamespaceReservationTraceContent.Identifier(json.Value<string>("id")));
                    activity.SetTag("gen_ai.response.model", NamespaceReservationTraceContent.Identifier(json.Value<string>("model")));
                    foreach (var tokenName in new[] { "input_tokens", "output_tokens" })
                    {
                        var value = json["usage"]?[tokenName];
                        if (value?.Type == JTokenType.Integer && long.TryParse(value.ToString(), out var count) && count >= 0)
                        {
                            activity.SetTag("gen_ai.usage." + tokenName, count);
                        }
                    }

                    var webSearchCalls = (json["output"] as JArray)?.OfType<JObject>()
                        .Where(item => (string)item["type"] == "web_search_call")
                        .ToArray() ?? Array.Empty<JObject>();
                    activity.SetTag("namespace_reservation.web_search.called", webSearchCalls.Length > 0);
                    activity.SetTag("namespace_reservation.web_search.action_count", webSearchCalls.Length);
                    activity.SetTag("namespace_reservation.web_search.page_opened", webSearchCalls.Any(
                        item => (string)item["action"]?["type"] == "open_page" && (string)item["status"] == "completed"));
                    var requests = json["tool_usage"]?["web_search"]?["num_requests"];
                    if (requests?.Type == JTokenType.Integer && int.TryParse(requests.ToString(), out var requestCount) && requestCount >= 0)
                    {
                        activity.SetTag("namespace_reservation.web_search.request_count", requestCount);
                    }
                });
                Content("gen_ai.output.messages", () =>
                {
                    // Omit unparsed/unexpected output. Never inspect or export reasoning items.
                    if (!(json["output"] is JArray output))
                    {
                        return null;
                    }

                    var call = output.OfType<JObject>().SingleOrDefault(item => (string)item["type"] == "function_call");
                    if (call != null)
                    {
                        var name = NamespaceReservationTraceContent.ToolName((string)call["name"]);
                        var parsed = NamespaceReservationAssessmentContract.ParseObject((string)call["arguments"]);
                        var args = NamespaceReservationTraceContent.Arguments(name, parsed);
                        return args == null ? null : NamespaceReservationTraceContent.Message("assistant", new JObject
                        {
                            ["type"] = "tool_call", ["name"] = name,
                            ["id"] = NamespaceReservationTraceContent.Identifier((string)call["call_id"]), ["arguments"] = args
                        });
                    }

                    var message = output.OfType<JObject>().SingleOrDefault(item => (string)item["type"] == "message");
                    if ((string)message?["role"] != "assistant" || (string)message?["status"] != "completed"
                        || !(message?["content"] is JArray parts) || parts.Count != 1 || (string)parts[0]["type"] != "output_text")
                    {
                        return null;
                    }

                    // This validation is telemetry-only. The client still validates against the
                    // actual delivered citations before accepting a decision or running its guard.
                    var result = NamespaceReservationAssessmentContract.Parse(new NamespaceReservationFoundryResponse((string)parts[0]["text"], null, null),
                        NamespaceReservationFoundryClient.CoreTools.Concat(new[] { "get_package_details" }));
                    return NamespaceReservationTraceContent.Message("assistant", NamespaceReservationTraceContent.TextPart(NamespaceReservationTraceContent.Decision(result)));
                });
            }

            internal void Input(NamespaceReservationAssessmentInput input, string feedbackName = null, string callId = null, JObject feedback = null)
            {
                Content("gen_ai.input.messages", () => input != null
                    ? NamespaceReservationTraceContent.Message("user", NamespaceReservationTraceContent.TextPart(NamespaceReservationTraceContent.Form(input)))
                    : feedback == null ? null : NamespaceReservationTraceContent.Message("tool", new JObject
                    {
                        ["type"] = "tool_call_response", ["id"] = NamespaceReservationTraceContent.Identifier(callId),
                        ["response"] = NamespaceReservationTraceContent.ToolResult(feedbackName, feedback)
                    }));
            }

            internal void ToolArguments(string name, JObject validated)
            {
                Content("gen_ai.tool.call.arguments", () => NamespaceReservationTraceContent.Arguments(name, validated));
            }

            internal void ToolResult(string name, JObject result)
            {
                Update(activity =>
                {
                    var complete = result?["complete"]?.Type == JTokenType.Boolean && (bool)result["complete"];
                    activity.SetTag("namespace_reservation.tool.complete", complete);
                    if (!complete || result["error"] != null)
                    {
                        Error();
                    }
                });
                Content("gen_ai.tool.call.result", () => NamespaceReservationTraceContent.ToolResult(name, result));
                Complete();
            }

            private void Content(string attribute, Func<JToken> project)
            {
                if (!_captureContent || _activity == null)
                {
                    return;
                }

                Safe(() =>
                {
                    var value = project();
                    if (value == null)
                    {
                        return;
                    }

                    var json = NamespaceReservationTraceContent.Bounded(value, out var truncated);
                    var bytes = Encoding.UTF8.GetByteCount(json);
                    if (_contentBytes + bytes >= 24000)
                    {
                        _activity.SetTag("namespace_reservation.content.truncated", true);
                        return;
                    }

                    _contentBytes += bytes;
                    _activity.SetTag(attribute, json);
                    if (truncated)
                    {
                        _activity.SetTag("namespace_reservation.content.truncated", true);
                    }
                });
            }

            public void Dispose()
            {
                if (_activity == null || !_ownsActivity || _disposed)
                {
                    return;
                }

                _disposed = true;
                if (!_completed)
                {
                    Error();
                }

                Safe(() => _activity.Dispose());
                Restore(_previous);
            }
        }
    }
}