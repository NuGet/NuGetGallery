// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    /// <summary>
    /// Assessment and connection-test client for the local POC. Uses the supported Responses REST API and the
    /// existing Azure SDK pipeline so no new OpenAI SDK dependency upgrades are needed.
    /// https://learn.microsoft.com/azure/foundry/openai/how-to/responses
    /// </summary>
    public class NamespaceReservationFoundryClient : INamespaceReservationFoundryClient
    {
        public const string TokenScope = "https://ai.azure.com/.default";
        internal const int MaxResponseBytes = 128 * 1024;
        internal const int MaxToolCalls = 5;
        internal const int MaxModelCalls = MaxToolCalls + 1;
        internal const int MaxToolResultCharacters = 32 * 1024;
        internal const int MaxConversationBytes = 256 * 1024;
        internal const int MaxWebsiteDomains = 10;
        internal const int MaxWebSearchActions = 8;
        internal const string WebSearchEvidenceReference = "web_search";

        private static readonly Regex _httpsUrlPattern = new Regex(@"https://[^\s<>\""']+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static readonly string[] CoreTools =
        {
            "get_request_account_facts", "get_namespace_reservations", "get_namespace_package_usage"
        };

        private readonly HttpPipeline _pipeline;
        private readonly Uri _endpoint;
        private readonly string _deploymentName;
        private readonly int _maxOutputTokens;
        private readonly TimeSpan _timeout;
        private readonly NamespaceReservationTracing _tracing;

        public bool IsEnabled { get; }

        public static NamespaceReservationFoundryClient Create(IAppConfiguration configuration, NamespaceReservationTracing tracing = null)
        {
            ValidateConfiguration(configuration);
            TokenCredential credential = null;
            if (configuration.NamespaceReservationFoundryEnabled)
            {
                // Do not use DefaultAzureCredential: the local work account may be unrelated to this resource.
                if (configuration.NamespaceReservationFoundryCredential == "VisualStudio")
                {
                    credential = new VisualStudioCredential(new VisualStudioCredentialOptions
                    {
                        TenantId = configuration.NamespaceReservationFoundryTenantId,
                        ProcessTimeout = TimeSpan.FromSeconds(configuration.NamespaceReservationFoundryTimeoutSeconds)
                    });
                }
                else
                {
                    // Honors the launching process's AZURE_CONFIG_DIR. Never change process-wide credentials here.
                    credential = new AzureCliCredential(new AzureCliCredentialOptions
                    {
                        TenantId = configuration.NamespaceReservationFoundryTenantId,
                        ProcessTimeout = TimeSpan.FromSeconds(configuration.NamespaceReservationFoundryTimeoutSeconds)
                    });
                }
            }

            return new NamespaceReservationFoundryClient(configuration, credential, tracing: tracing);
        }

        public NamespaceReservationFoundryClient(
            IAppConfiguration configuration,
            TokenCredential credential,
            HttpPipelineTransport transport = null,
            NamespaceReservationTracing tracing = null)
        {
            _tracing = tracing ?? NamespaceReservationTracing.Disabled;
            ValidateConfiguration(configuration);
            IsEnabled = configuration.NamespaceReservationFoundryEnabled;
            if (!IsEnabled)
            {
                return;
            }

            if (credential == null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            _endpoint = new Uri(configuration.NamespaceReservationFoundryEndpoint.TrimEnd('/') + "/responses");
            _deploymentName = configuration.NamespaceReservationFoundryDeploymentName;
            _maxOutputTokens = configuration.NamespaceReservationFoundryMaxOutputTokens;
            _timeout = TimeSpan.FromSeconds(configuration.NamespaceReservationFoundryTimeoutSeconds);

            var options = new FoundryClientOptions();
            options.Retry.MaxRetries = 0;
            options.Retry.NetworkTimeout = _timeout;
            options.Diagnostics.IsLoggingContentEnabled = false;
            if (transport != null)
            {
                options.Transport = transport;
            }

            _pipeline = HttpPipelineBuilder.Build(options, new BearerTokenAuthenticationPolicy(credential, TokenScope));
        }

        public Task<NamespaceReservationFoundryResponse> TestConnectionAsync(CancellationToken cancellationToken)
        {
            return SendAsync(new JObject { ["input"] = "Reply with exactly: Connection successful." }, cancellationToken);
        }

        public async Task<NamespaceReservationAssessment> AssessAsync(NamespaceReservationAssessmentInput input, CancellationToken cancellationToken)
        {
            using (var scope = _tracing.StartAssessment())
            {
                try
                {
                    var result = await AssessSubmissionAsync(input, cancellationToken);
                    scope.Decision(result, result, 0);
                    return result;
                }
                catch (Exception ex)
                {
                    scope.Error(ex);
                    throw;
                }
            }
        }

        private async Task<NamespaceReservationAssessment> AssessSubmissionAsync(NamespaceReservationAssessmentInput input, CancellationToken cancellationToken)
        {
            EnsureEnabled();
            cancellationToken.ThrowIfCancellationRequested();
            var serializedInput = NamespaceReservationAssessmentContract.SerializeInput(input);
            var response = await SendAsync(new JObject
            {
                ["instructions"] = NamespaceReservationAssessmentContract.Skill,
                ["input"] = new JArray(new JObject
                {
                    ["role"] = "user",
                    ["content"] = new JArray(new JObject
                    {
                        ["type"] = "input_text",
                        ["text"] = serializedInput
                    })
                }),
                ["text"] = new JObject { ["format"] = NamespaceReservationAssessmentContract.CreateResponseFormat() }
            }, cancellationToken, input);

            cancellationToken.ThrowIfCancellationRequested();
            return NamespaceReservationAssessmentContract.Parse(response);
        }

        public async Task<NamespaceReservationAssessment> AssessAsync(
            NamespaceReservationAssessmentInput input,
            INamespaceReservationEvidenceSession evidence,
            CancellationToken cancellationToken)
        {
            using (var scope = _tracing.StartAssessment())
            {
                try
                {
                    return await AssessWithEvidenceAsync(input, evidence, cancellationToken, scope);
                }
                catch (Exception ex)
                {
                    scope.Error(ex);
                    throw;
                }
            }
        }

        private async Task<NamespaceReservationAssessment> AssessWithEvidenceAsync(
            NamespaceReservationAssessmentInput input,
            INamespaceReservationEvidenceSession evidence,
            CancellationToken cancellationToken,
            NamespaceReservationTracing.Scope scope)
        {
            EnsureEnabled();
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            cancellationToken.ThrowIfCancellationRequested();
            // Only the form is serialized. The session holds canonical identity/scope server-side.
            var conversation = new JArray(new JObject
            {
                ["role"] = "user",
                ["content"] = new JArray(new JObject
                {
                    ["type"] = "input_text",
                    ["text"] = NamespaceReservationAssessmentContract.SerializeInput(input)
                })
            });
            var delivered = new Dictionary<string, JObject>(StringComparer.Ordinal);
            var websiteDomains = GetWebsiteDomains(input.Justification);
            var webSearchAttempted = false;
            var webSearchDelivered = false;
            var callIds = new HashSet<string>(StringComparer.Ordinal);
            var attempts = 0;
            JObject feedback = null;
            string feedbackName = null;
            string feedbackCallId = null;
            long? inputTokens = 0;
            long? outputTokens = 0;
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                // One deadline across authentication, every model turn, and every SQL read.
                deadline.CancelAfter(TimeSpan.FromSeconds(Math.Min(60, _timeout.TotalSeconds)));
                for (var turn = 0; turn < MaxModelCalls; turn++)
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    var allowTools = attempts < MaxToolCalls && turn < MaxModelCalls - 1;
                    var allowWebSearch = allowTools && !webSearchAttempted && websiteDomains.Count > 0;
                    var availableReferences = delivered.Keys.Concat(
                        allowWebSearch || webSearchDelivered ? new[] { WebSearchEvidenceReference } : Array.Empty<string>());
                    var body = new JObject
                    {
                        ["instructions"] = NamespaceReservationAssessmentContract.Skill,
                        ["input"] = conversation.DeepClone(),
                        ["parallel_tool_calls"] = false,
                        ["tool_choice"] = allowTools ? "auto" : "none",
                        ["include"] = allowWebSearch
                            ? new JArray("reasoning.encrypted_content", "web_search_call.action.sources")
                            : new JArray("reasoning.encrypted_content"),
                        ["text"] = new JObject { ["format"] = NamespaceReservationAssessmentContract.CreateResponseFormat(availableReferences) }
                    };
                    if (allowTools)
                    {
                        body["tools"] = CreateTools(allowWebSearch ? websiteDomains : null);
                    }

                    var json = await SendJsonAsync(body, deadline.Token, turn + 1,
                        turn == 0 ? input : null, feedbackName, feedbackCallId, feedback);
                    ValidateEnvelope(json, allowTools, allowWebSearch, websiteDomains);
                    inputTokens = checked(inputTokens + ReadUsage(json, "input_tokens"));
                    outputTokens = checked(outputTokens + ReadUsage(json, "output_tokens"));
                    var output = (JArray)json["output"];
                    webSearchAttempted |= output.OfType<JObject>().Any(item => TextValue(item["type"]) == "web_search_call");
                    var webSearchUsed = ValidateWebSearch(output, allowWebSearch, websiteDomains);
                    webSearchDelivered |= webSearchUsed;
                    var calls = output.OfType<JObject>().Where(item => TextValue(item["type"]) == "function_call").ToArray();
                    if (calls.Length == 0)
                    {
                        var text = ParseResponse(json, allowWebSearch, websiteDomains);
                        var deliveredReferences = delivered.Keys.Concat(
                            webSearchDelivered ? new[] { WebSearchEvidenceReference } : Array.Empty<string>());
                        var result = NamespaceReservationAssessmentContract.Parse(
                            new NamespaceReservationFoundryResponse(text.Text, inputTokens, outputTokens), deliveredReferences);
                        deadline.Token.ThrowIfCancellationRequested();
                        var accepted = RequireCoreEvidence(result, delivered);
                        scope.Decision(result, accepted, attempts);
                        return accepted;
                    }

                    // parallel_tool_calls=false: reject unexpected batches/mixed decisions before SQL.
                    if (!allowTools || calls.Length != 1 || output.Any(item => TextValue(item["type"]) == "message"))
                    {
                        throw NamespaceReservationAssessmentContract.InvalidResponse();
                    }

                    var call = calls[0];
                    var callId = TextValue(call["call_id"]);
                    var name = TextValue(call["name"]);
                    var arguments = TextValue(call["arguments"]);
                    if (string.IsNullOrWhiteSpace(callId) || callId.Length > 128 || callId.Any(char.IsControl)
                        || !callIds.Add(callId) || string.IsNullOrWhiteSpace(name) || name.Length > 128
                        || arguments == null || arguments.Length > 4096
                        || (call["status"] != null && TextValue(call["status"]) != "completed"))
                    {
                        throw NamespaceReservationAssessmentContract.InvalidResponse();
                    }

                    // Count before dispatch/argument validation. Invalid, failed and repeated calls
                    // consume the same budget. No inference or SQL retries reset this counter.
                    attempts++;
                    var toolResult = await ExecuteToolAsync(evidence, name, arguments, deadline.Token, callId, attempts);
                    feedback = toolResult;
                    feedbackName = name;
                    feedbackCallId = callId;
                    deadline.Token.ThrowIfCancellationRequested();
                    var resultText = toolResult.ToString(Formatting.None);
                    if (resultText.Length > MaxToolResultCharacters)
                    {
                        throw NamespaceReservationAssessmentContract.InvalidResponse();
                    }

                    if (CoreTools.Contains(name) || name == "get_package_details")
                    {
                        // A later failed read must invalidate earlier evidence of the same kind.
                        delivered.Remove(name);
                        if (toolResult["error"] == null)
                        {
                            delivered[name] = (JObject)toolResult.DeepClone();
                        }
                    }

                    // Stateless Responses calls must preserve reasoning items, including encrypted
                    // content, alongside the exact call/output association. Never log this history.
                    foreach (var item in output.Where(item => TextValue(item["type"]) == "reasoning" || TextValue(item["type"]) == "web_search_call"))
                    {
                        conversation.Add(item.DeepClone());
                    }

                    conversation.Add(new JObject
                    {
                        ["type"] = "function_call", ["call_id"] = callId,
                        ["name"] = name, ["arguments"] = arguments
                    });
                    conversation.Add(new JObject
                    {
                        ["type"] = "function_call_output", ["call_id"] = callId, ["output"] = resultText
                    });
                }
            }

            throw NamespaceReservationAssessmentContract.InvalidResponse();
        }

        internal static JArray CreateTools(IReadOnlyCollection<string> websiteDomains = null)
        {
            var descriptions = new[]
            {
                "Read confirmed-account, exact Microsoft-domain eligibility and organization-admin facts for this request only. No arguments.",
                "Read existing exact, parent and overlapping reservation aggregates and up to 20 samples for this request only. No arguments.",
                "Read complete current matching-package ownership/listing aggregates and up to 20 samples. Stored timestamps are not verified first-publication history. No arguments.",
                "Read details for 1-10 distinct package IDs already returned by get_namespace_package_usage in this assessment. No other IDs are allowed."
            };
            var names = CoreTools.Concat(new[] { "get_package_details" }).ToArray();
            var tools = new JArray(names.Select((name, index) => new JObject
            {
                ["type"] = "function", ["name"] = name, ["description"] = descriptions[index], ["strict"] = true,
                ["parameters"] = new JObject
                {
                    ["type"] = "object", ["additionalProperties"] = false,
                    ["properties"] = index < 3 ? new JObject() : new JObject
                    {
                        ["packageIds"] = new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "string" } }
                    },
                    ["required"] = index < 3 ? new JArray() : new JArray("packageIds")
                }
            }));

            if (websiteDomains != null && websiteDomains.Count > 0)
            {
                tools.Add(new JObject
                {
                    ["type"] = "web_search",
                    ["filters"] = new JObject
                    {
                        ["allowed_domains"] = new JArray(websiteDomains)
                    }
                });
            }

            return tools;
        }

        internal static IReadOnlyCollection<string> GetWebsiteDomains(string justification)
        {
            if (string.IsNullOrWhiteSpace(justification))
            {
                return Array.Empty<string>();
            }

            return _httpsUrlPattern.Matches(justification)
                .Cast<Match>()
                .Select(match => match.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '}'))
                .Select(value => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null)
                .Where(uri => uri != null
                    && uri.Scheme == Uri.UriSchemeHttps
                    && uri.IsDefaultPort
                    && string.IsNullOrEmpty(uri.UserInfo)
                    && Uri.CheckHostName(uri.DnsSafeHost) == UriHostNameType.Dns
                    && uri.DnsSafeHost.Contains(".")
                    && uri.DnsSafeHost.All(character => character <= 127))
                .Select(uri => uri.DnsSafeHost.TrimEnd('.').ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .Take(MaxWebsiteDomains)
                .ToArray();
        }

        private async Task<JObject> ExecuteToolAsync(INamespaceReservationEvidenceSession evidence, string name, string arguments, CancellationToken token, string callId, int attempt)
        {
            using (var scope = _tracing.StartTool(name, callId, attempt))
            {
                try
                {
                    var result = await ExecuteToolCoreAsync(evidence, name, arguments, token, scope);
                    scope.ToolResult(name, result);
                    return result;
                }
                catch (Exception ex)
                {
                    scope.Error(ex);
                    throw;
                }
            }
        }

        private static async Task<JObject> ExecuteToolCoreAsync(INamespaceReservationEvidenceSession evidence, string name, string arguments, CancellationToken token, NamespaceReservationTracing.Scope scope)
        {
            if (!CoreTools.Contains(name, StringComparer.Ordinal) && name != "get_package_details")
            {
                return ToolFailure("tool_not_allowed");
            }

            JObject parsed;
            try
            {
                parsed = NamespaceReservationAssessmentContract.ParseObject(arguments);
            }
            catch (InvalidOperationException)
            {
                return ToolFailure("invalid_tool_arguments");
            }

            // Validate here as well as at the SQL boundary. No unexpected fields reach a tool.
            if (!TryReadToolArguments(name, parsed, out _))
            {
                return ToolFailure("invalid_tool_arguments");
            }

            scope.ToolArguments(name, parsed);
            try
            {
                var result = await evidence.ExecuteAsync(name, parsed, token);
                return result != null && result["complete"]?.Type == JTokenType.Boolean
                    ? result : ToolFailure("evidence_unavailable");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return ToolFailure("evidence_unavailable");
            }
        }

        private static JObject ToolFailure(string code)
        {
            return new JObject { ["complete"] = false, ["error"] = code };
        }

        internal static bool TryReadToolArguments(string name, JObject parsed, out JObject validated)
        {
            validated = null;
            if (parsed == null || (!CoreTools.Contains(name, StringComparer.Ordinal) && name != "get_package_details")
                || (name != "get_package_details" ? parsed.Count != 0
                    : parsed.Count != 1 || !(parsed["packageIds"] is JArray ids) || ids.Count < 1 || ids.Count > 10
                        || ids.Any(id => id.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)id)
                            || ((string)id).Length > 128 || ((string)id).Any(char.IsControl))
                        || ids.Values<string>().Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Count))
            {
                return false;
            }

            validated = parsed;
            return true;
        }

        private static NamespaceReservationAssessment RequireCoreEvidence(NamespaceReservationAssessment result, IDictionary<string, JObject> delivered)
        {
            if (result.Status != "Approved")
            {
                return result;
            }

            if (CoreTools.All(name => delivered.TryGetValue(name, out var facts) && facts["complete"]?.Type == JTokenType.Boolean
                && (bool)facts["complete"] && result.EvidenceReferences.Contains(name)))
            {
                return result;
            }

            const string reason = "Required account, reservation, or package evidence is missing or incomplete.";
            return new NamespaceReservationAssessment("Rejected", "evidence_unavailable", reason,
                "The host did not accept approval because required checks remain unresolved.",
                new[] { "IDENTITY", "RESERVATIONS" }, result.EvidenceReferences.ToArray(), new[] { reason }, result.InputTokens, result.OutputTokens);
        }

        private void EnsureEnabled()
        {
            if (!IsEnabled)
            {
                throw new InvalidOperationException("The namespace reservation Foundry client is disabled.");
            }
        }

        private async Task<NamespaceReservationFoundryResponse> SendAsync(JObject body, CancellationToken cancellationToken, NamespaceReservationAssessmentInput input = null)
        {
            return ParseResponse(await SendJsonAsync(body, cancellationToken, input: input));
        }

        private async Task<JObject> SendJsonAsync(JObject body, CancellationToken cancellationToken, int turn = 1,
            NamespaceReservationAssessmentInput input = null, string feedbackName = null, string feedbackCallId = null, JObject feedback = null)
        {
            var webSearchEnabled = body["tools"] is JArray tools
                && tools.OfType<JObject>().Any(tool => TextValue(tool["type"]) == "web_search");
            using (var scope = _tracing.StartModel(_deploymentName, turn, _maxOutputTokens, TextValue(body["tool_choice"]), webSearchEnabled))
            {
                scope.Input(input, feedbackName, feedbackCallId, feedback);
                try
                {
                    var json = await SendJsonCoreAsync(body, cancellationToken, scope);
                    scope.Response(json);
                    scope.Complete();
                    return json;
                }
                catch (Exception ex)
                {
                    scope.Error(ex);
                    throw;
                }
            }
        }

        private async Task<JObject> SendJsonCoreAsync(JObject body, CancellationToken cancellationToken, NamespaceReservationTracing.Scope scope)
        {
            EnsureEnabled();
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            using (var message = _pipeline.CreateMessage())
            {
                timeout.CancelAfter(_timeout);
                timeout.Token.ThrowIfCancellationRequested();
                body["model"] = _deploymentName;
                body["max_output_tokens"] = _maxOutputTokens;
                body["store"] = false;
                // Read under our own byte bound and cancellation token instead of unbounded pipeline buffering.
                message.BufferResponse = false;
                message.Request.Method = RequestMethod.Post;
                message.Request.Uri.Reset(_endpoint);
                message.Request.Headers.Add("Content-Type", "application/json");
                var requestJson = body.ToString(Formatting.None);
                if (Encoding.UTF8.GetByteCount(requestJson) > MaxConversationBytes)
                {
                    throw NamespaceReservationAssessmentContract.InvalidResponse();
                }

                message.Request.Content = RequestContent.Create(requestJson);

                await _pipeline.SendAsync(message, timeout.Token);
                scope.HttpStatus(message.Response.Status);
                if (message.Response.Status != 200)
                {
                    // Do not expose raw service responses, credentials, or customer inputs in errors.
                    throw new RequestFailedException(message.Response.Status, "The Foundry request failed. No decision was accepted.");
                }

                var json = NamespaceReservationAssessmentContract.ParseObject(await ReadResponseAsync(message.Response.ContentStream, timeout.Token));
                timeout.Token.ThrowIfCancellationRequested();
                return json;
            }
        }

        private static async Task<string> ReadResponseAsync(Stream stream, CancellationToken cancellationToken)
        {
            if (stream == null)
            {
                throw NamespaceReservationAssessmentContract.InvalidResponse();
            }

            using (var buffer = new MemoryStream())
            {
                var chunk = new byte[4096];
                int count;
                while ((count = await stream.ReadAsync(chunk, 0, chunk.Length, cancellationToken)) != 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (buffer.Length + count > MaxResponseBytes)
                    {
                        throw NamespaceReservationAssessmentContract.InvalidResponse();
                    }

                    buffer.Write(chunk, 0, count);
                }

                return Encoding.UTF8.GetString(buffer.ToArray());
            }
        }

        private static void ValidateEnvelope(
            JObject json,
            bool allowFunctions,
            bool allowWebSearch = false,
            IReadOnlyCollection<string> websiteDomains = null)
        {
            if (TextValue(json["status"]) != "completed"
                || (json["error"] != null && json["error"].Type != JTokenType.Null)
                || (json["incomplete_details"] != null && json["incomplete_details"].Type != JTokenType.Null)
                || !(json["output"] is JArray output)
                || output.Any(item => !(item is JObject obj) || (TextValue(obj["type"]) != "message" && TextValue(obj["type"]) != "reasoning"
                    && !(allowFunctions && TextValue(obj["type"]) == "function_call")
                    && !(allowWebSearch && TextValue(obj["type"]) == "web_search_call"))))
            {
                throw NamespaceReservationAssessmentContract.InvalidResponse();
            }

            ValidateWebSearch(output, allowWebSearch, websiteDomains ?? Array.Empty<string>());

            if (json["content_filters"] != null && json["content_filters"].Type != JTokenType.Null)
            {
                if (!(json["content_filters"] is JArray filters)
                    || filters.Any(item => !(item is JObject filter) || filter["blocked"]?.Type != JTokenType.Boolean || (bool)filter["blocked"]))
                {
                    throw NamespaceReservationAssessmentContract.InvalidResponse();
                }
            }

            ReadUsage(json, "input_tokens");
            ReadUsage(json, "output_tokens");
        }

        private static NamespaceReservationFoundryResponse ParseResponse(
            JObject json,
            bool allowWebSearch = false,
            IReadOnlyCollection<string> websiteDomains = null)
        {
            ValidateEnvelope(json, allowFunctions: false, allowWebSearch: allowWebSearch, websiteDomains: websiteDomains);
            var output = (JArray)json["output"];

            var messages = output.OfType<JObject>().Where(item => TextValue(item["type"]) == "message").ToArray();
            if (messages.Length != 1 || TextValue(messages[0]["role"]) != "assistant"
                || TextValue(messages[0]["status"]) != "completed"
                || !(messages[0]["content"] is JArray content) || content.Count != 1
                || !(content[0] is JObject part) || TextValue(part["type"]) != "output_text"
                || string.IsNullOrWhiteSpace(TextValue(part["text"])))
            {
                // Refusal, mixed refusal/text, tool requests, and multiple answers are not decisions.
                throw NamespaceReservationAssessmentContract.InvalidResponse();
            }

            return new NamespaceReservationFoundryResponse(TextValue(part["text"]), ReadUsage(json, "input_tokens"), ReadUsage(json, "output_tokens"));
        }

        private static bool ValidateWebSearch(JArray output, bool allowWebSearch, IReadOnlyCollection<string> websiteDomains)
        {
            var calls = output.OfType<JObject>().Where(item => TextValue(item["type"]) == "web_search_call").ToArray();
            if (calls.Length == 0)
            {
                return false;
            }

            if (!allowWebSearch || websiteDomains == null || websiteDomains.Count == 0 || calls.Length > MaxWebSearchActions)
            {
                throw NamespaceReservationAssessmentContract.InvalidResponse();
            }

            var openedAllowedPage = false;
            foreach (var call in calls)
            {
                if (TextValue(call["status"]) != "completed" || !(call["action"] is JObject action))
                {
                    throw NamespaceReservationAssessmentContract.InvalidResponse();
                }

                var actionType = TextValue(action["type"]);
                if (actionType != "search" && actionType != "open_page" && actionType != "find_in_page")
                {
                    throw NamespaceReservationAssessmentContract.InvalidResponse();
                }

                if (actionType == "open_page")
                {
                    if (!IsAllowedWebsiteUrl(TextValue(action["url"]), websiteDomains))
                    {
                        throw NamespaceReservationAssessmentContract.InvalidResponse();
                    }

                    openedAllowedPage = true;
                }

                if (action["sources"] != null)
                {
                    if (!(action["sources"] is JArray sources)
                        || sources.Any(source => !(source is JObject sourceObject)
                            || !IsAllowedWebsiteUrl(TextValue(sourceObject["url"]), websiteDomains)))
                    {
                        throw NamespaceReservationAssessmentContract.InvalidResponse();
                    }
                }
            }

            foreach (var message in output.OfType<JObject>().Where(item => TextValue(item["type"]) == "message"))
            {
                if (!(message["content"] is JArray content))
                {
                    continue;
                }

                var citations = content.OfType<JObject>()
                    .SelectMany(part => (part["annotations"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
                    .Where(annotation => TextValue(annotation["type"]) == "url_citation")
                    .ToArray();
                if (citations.Any(citation => !IsAllowedWebsiteUrl(TextValue(citation["url"]), websiteDomains)))
                {
                    throw NamespaceReservationAssessmentContract.InvalidResponse();
                }
            }

            // A search result alone is not treated as visiting the customer-supplied website.
            return openedAllowedPage;
        }

        private static bool IsAllowedWebsiteUrl(string value, IReadOnlyCollection<string> websiteDomains)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || !uri.IsDefaultPort
                || !string.IsNullOrEmpty(uri.UserInfo))
            {
                return false;
            }

            var host = uri.DnsSafeHost.TrimEnd('.');
            return websiteDomains.Any(domain => string.Equals(host, domain, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));
        }

        private static string TextValue(JToken token)
        {
            return token?.Type == JTokenType.String ? (string)token : null;
        }

        private static long? ReadUsage(JObject json, string name)
        {
            if (json["usage"] == null || json["usage"].Type == JTokenType.Null)
            {
                return null;
            }

            if (!(json["usage"] is JObject usage))
            {
                throw NamespaceReservationAssessmentContract.InvalidResponse();
            }

            var token = usage[name];
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            if (token.Type != JTokenType.Integer
                || !long.TryParse(token.ToString(Formatting.None), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                throw NamespaceReservationAssessmentContract.InvalidResponse();
            }

            return value;
        }

        private sealed class FoundryClientOptions : ClientOptions
        {
        }

        private static void ValidateConfiguration(IAppConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (!configuration.NamespaceReservationFoundryEnabled)
            {
                return;
            }

            if (configuration.Environment != ServicesConstants.DevelopmentEnvironment)
            {
                throw new ConfigurationErrorsException("The namespace reservation Foundry client is for local development only.");
            }

            if (!Uri.TryCreate(configuration.NamespaceReservationFoundryEndpoint, UriKind.Absolute, out var endpoint)
                || endpoint.Scheme != Uri.UriSchemeHttps
                || !endpoint.IsDefaultPort
                || !string.IsNullOrEmpty(endpoint.UserInfo)
                || !string.IsNullOrEmpty(endpoint.Query)
                || !string.IsNullOrEmpty(endpoint.Fragment)
                || endpoint.AbsolutePath.TrimEnd('/') != "/openai/v1"
                || !(endpoint.Host.EndsWith(".services.ai.azure.com", StringComparison.OrdinalIgnoreCase)
                    || endpoint.Host.EndsWith(".openai.azure.com", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ConfigurationErrorsException("Foundry requires an Azure HTTPS model base endpoint ending in /openai/v1/, not /responses or a project endpoint.");
            }

            if (string.IsNullOrWhiteSpace(configuration.NamespaceReservationFoundryDeploymentName)
                || !Guid.TryParse(configuration.NamespaceReservationFoundryTenantId, out var tenantId)
                || tenantId == Guid.Empty)
            {
                throw new ConfigurationErrorsException("Foundry requires a deployment name and an explicit tenant ID.");
            }

            if (configuration.NamespaceReservationFoundryCredential != "VisualStudio"
                && configuration.NamespaceReservationFoundryCredential != "AzureCli")
            {
                throw new ConfigurationErrorsException("The Foundry credential must be VisualStudio or AzureCli.");
            }

            if (configuration.NamespaceReservationFoundryMaxOutputTokens < 1
                || configuration.NamespaceReservationFoundryMaxOutputTokens > 4096
                || configuration.NamespaceReservationFoundryTimeoutSeconds < 1
                || configuration.NamespaceReservationFoundryTimeoutSeconds > 120)
            {
                throw new ConfigurationErrorsException("Foundry limits must be 1-4096 output tokens and 1-120 seconds.");
            }
        }
    }
}