using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text;
using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Model.WebsocketMessages.ServerMessages;
using Voxta.Providers.Host;

namespace Voxta.VoxtaMCPBridge
{
    public class MCPBridgeProvider : ProviderBase
    {
        private readonly ILogger<MCPBridgeProvider> _logger;
        private readonly IConfiguration _configuration;
        private Process _mcpClientProcess;
        private StreamReader? _mcpClientOutput;
        private StreamWriter? _mcpClientInput;
        private readonly string _mcpClientScriptPath;
        private readonly string _pythonExePath;
        private readonly string _actionConfigPath;

        public class McpResponse
        {
            public string? Result { get; set; }
            public string? Error { get; set; }
        }

        public class ToolInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public Dictionary<string, object> Parameters { get; set; } = new();
        }

        public class ActionConfig
        {
            public List<ToolInfo> Tools { get; set; } = new();
        }

        public class ToolDefinition
        {
            public string Name { get; set; } = string.Empty;
            public string Layer { get; set; } = "notion";
            public string Description { get; set; } = string.Empty;
            public ActionEffect? Effect { get; set; }
            public List<JsonArgument>? Arguments { get; set; }
        }

        public class JsonArgument
        {
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = "string";
            public bool Required { get; set; } = false;
            public string Description { get; set; } = string.Empty;
        }

        public MCPBridgeProvider(IRemoteChatSession session, ILogger<MCPBridgeProvider> logger, IConfiguration configuration)
            : base(session, logger)
        {
            _logger = logger;
            _configuration = configuration;
            _mcpClientProcess = new();

            // Get configuration values
            _mcpClientScriptPath = _configuration.GetValue<string>("MCPBridge:MCPClientScriptPath") ?? string.Empty;
            _pythonExePath = _configuration.GetValue<string>("MCPBridge:PythonExePath") ?? "python3";
            _actionConfigPath = _configuration.GetValue<string>("MCPBridge:ActionConfigPath") ?? string.Empty;

            if (string.IsNullOrEmpty(_mcpClientScriptPath))
            {
                _logger.LogError("MCPClientScriptPath is not configured in appsettings.json");
                throw new InvalidOperationException("MCPClientScriptPath is required");
            }

            if (string.IsNullOrEmpty(_actionConfigPath))
            {
                _logger.LogError("ActionConfigPath is not configured in appsettings.json");
                throw new InvalidOperationException("ActionConfigPath is required");
            }
        }

        protected override async Task OnStartAsync()
        {
            await base.OnStartAsync();
            _logger.LogInformation("Starting MCPBridgeProvider...");

            var pythonExePath = _configuration.GetValue<string>("MCPBridge:PythonExePath") ?? "python3";
            var mcpClientScriptPath = _configuration.GetValue<string>("MCPBridge:MCPClientScriptPath");

            if (string.IsNullOrEmpty(mcpClientScriptPath))
            {
                _logger.LogError("MCPClientScriptPath is not configured");
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _pythonExePath,
                Arguments = _mcpClientScriptPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardInputEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
            };

            try
            {
                _mcpClientProcess.StartInfo = startInfo;
                if (!_mcpClientProcess.Start())
                {
                    _logger.LogError("Failed to start Python MCP client process");
                    return;
                }
                _mcpClientOutput = _mcpClientProcess.StandardOutput;
                _mcpClientInput = _mcpClientProcess.StandardInput;

                _mcpClientProcess.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        _logger.LogError("Python MCP client error: {Error}", args.Data);
                    }
                };
                _mcpClientProcess.BeginErrorReadLine();

                // Load tools from JSON file
                var actionJson = await File.ReadAllTextAsync(_actionConfigPath);
                var actionConfig = JsonConvert.DeserializeObject<ActionConfig>(actionJson);

                if (actionConfig?.Tools == null || !actionConfig.Tools.Any())
                {
                    _logger.LogError("No tools found in configuration file");
                    throw new InvalidOperationException("No tools found in configuration file");
                }

                // Register tools with Voxta using ScenarioActionDefinition
                Send(new ClientUpdateContextMessage
                {
                    SessionId = SessionId,
                    ContextKey = "NotionActions",
                    Actions = actionConfig.Tools.Select(tool => new ScenarioActionDefinition
                    {
                        Name = tool.Name,
                        Layer = "notion",
                        Description = tool.Description,
                        Arguments = new[]
                        {
                            new FunctionArgumentDefinition
                            {
                                Name = "block_id",
                                Type = FunctionArgumentType.String,
                                Required = true,
                                Description = "The ID of the parent block"
                            }
                        }
                    }).ToArray()
                });

                HandleMessage<ServerActionMessage>(message =>
                {
                    if (message.Layer != "notion")
                        return;

                    _logger.LogInformation("Action triggered: {Name} (Layer: {Layer}, Role: {Role})",
                        message.Value, message.Layer, message.Role);

                    if (message.Role != ChatMessageRole.User)
                    {
                        _logger.LogWarning("Ignoring action from non-user role to prevent loops");
                        return;
                    }

                    if (_mcpClientInput == null || _mcpClientOutput == null)
                    {
                        _logger.LogError("Python MCP client is not properly initialized");
                        return;
                    }

                    try
                    {
                        Task.Run(async () =>
                        {
                            // Extra null-check in the lambda to satisfy the compiler.
                            if (_mcpClientInput == null || _mcpClientOutput == null)
                                return;

                            var mcpRequest = JsonConvert.SerializeObject(new
                            {
                                method = "call_tool",
                                @params = new
                                {
                                    name = message.Value,
                                    arguments = message.Arguments?.ToDictionary(
                                        arg => arg.Name ?? "",
                                        arg => (object)(arg.Value ?? "")
                                    ) ?? new Dictionary<string, object>()
                                }
                            });

                            await _mcpClientInput.WriteLineAsync(mcpRequest);
                            await _mcpClientInput.FlushAsync();

                            var mcpResponseJson = await _mcpClientOutput.ReadLineAsync();
                            if (string.IsNullOrEmpty(mcpResponseJson))
                            {
                                _logger.LogError("Received empty response from MCP client");
                                return;
                            }

                            var mcpResponse = JsonConvert.DeserializeObject<McpResponse>(mcpResponseJson);
                            var responseText = mcpResponse?.Error != null
                                ? $"Error from MCP tool: {mcpResponse.Error}"
                                : mcpResponse?.Result ?? "No result from MCP tool";

                            Send(new ClientSendMessage
                            {
                                SessionId = SessionId,
                                Text = responseText,
                                DoUserActionInference = false
                            });
                        }).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing Voxta action");
                    }
                });

                _logger.LogInformation("MCPBridgeProvider started successfully with {Count} actions", actionConfig.Tools.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting Python MCP client process");
            }
        }

        protected override async Task OnStopAsync()
        {
            _logger.LogInformation("Stopping MCPBridgeProvider...");

            try
            {
                if (_mcpClientProcess is not null && !_mcpClientProcess.HasExited)
                {
                    var quitCommand = JsonConvert.SerializeObject(new { method = "quit" });
                    if (_mcpClientInput != null)
                    {
                        await _mcpClientInput.WriteLineAsync(quitCommand);
                        await _mcpClientInput.FlushAsync();
                        _mcpClientInput.Close();
                    }

                    if (!_mcpClientProcess.WaitForExit(5000))
                    {
                        _mcpClientProcess.Kill();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during MCPBridgeProvider shutdown");
            }

            await base.OnStopAsync();
        }

        private Task RegisterTools(string toolsJson)
        {
            try
            {
                _logger.LogInformation($"Tools: {toolsJson}");

                var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(toolsJson);
                if (response == null || !response.ContainsKey("tools") || !(response["tools"] is JArray))
                {
                    _logger.LogError("Invalid tool list format received from Python agent.");
                    return Task.CompletedTask;
                }

                var tools = ((JArray)response["tools"]).ToObject<List<ToolInfo>>();
                if (tools == null)
                {
                    _logger.LogError("Unable to get tool information");
                    return Task.CompletedTask;
                }

                Send(new ClientUpdateContextMessage
                {
                    SessionId = SessionId,
                    ContextKey = "NotionActions",
                    Actions = tools.Select(tool => new ScenarioActionDefinition
                    {
                        Name = tool.Name,
                        Layer = "notion",
                        Description = tool.Description,
                        Arguments = MapArguments(tool.Parameters).ToArray()
                    }).ToArray()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while registering tools");
            }
            return Task.CompletedTask;
        }

        private List<FunctionArgumentDefinition> MapArguments(Dictionary<string, object> parameters)
        {
            var argumentDefinitions = new List<FunctionArgumentDefinition>();
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    if (param.Value is JObject jObject)
                    {
                        var dict = jObject.ToObject<Dictionary<string, object>>();
                        if (dict != null)
                        {
                            var type = dict.ContainsKey("type") ? dict["type"]?.ToString() : null;
                            var description = dict.ContainsKey("description") ? dict["description"]?.ToString() : null;
                            bool required = false;
                            if (dict.ContainsKey("required") && dict["required"] is bool reqValue)
                            {
                                required = reqValue;
                            }

                            argumentDefinitions.Add(new FunctionArgumentDefinition
                            {
                                Name = param.Key,
                                Type = ParseArgumentType(type ?? "string"),
                                Description = description ?? "",
                                Required = required
                            });
                        }
                    }
                }
            }
            return argumentDefinitions;
        }

        private static FunctionArgumentType ParseArgumentType(string typeStr)
        {
            return typeStr.ToLower() switch
            {
                "string"  => FunctionArgumentType.String,
                "number"  => FunctionArgumentType.String,   // Remapped to String
                "boolean" => FunctionArgumentType.String,   // Remapped to String
                _         => FunctionArgumentType.String
            };
        }
    }
}
