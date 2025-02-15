# Voxta MCP Bridge Provider

A Voxta provider that enables communication with Model Context Protocol (MCP) servers, allowing Voxta to leverage external tools and resources through the MCP protocol.

## Prerequisites

- .NET 8.0 SDK
- Python 3.10 or higher
- Git

## Setup Instructions

### Windows

1. Clone the repository:
   ```powershell
   git clone https://github.com/voxta/voxta-mcp.git
   cd voxta-mcp
   ```

2. Create Python virtual environment:
   ```powershell
   python -m venv venv
   .\venv\Scripts\activate
   pip install mcp-agent
   ```

3. Build and run the project:
   ```powershell
   dotnet build
   dotnet run
   ```

### Linux

1. Clone the repository:
   ```bash
   git clone https://github.com/voxta/voxta-mcp.git
   cd voxta-mcp
   ```

2. Create Python virtual environment:
   ```bash
   python3 -m venv venv
   source venv/bin/activate
   pip install mcp-agent
   ```

3. Build and run the project:
   ```bash
   dotnet build
   dotnet run
   ```

## Running the Provider

1. Ensure your MCP server is running and accessible at the configured address.

2. Configure `appsettings.json` with your settings:
   ```json
   {
     "MCPBridge": {
       "PythonExePath": "venv/bin/python",  // Path to Python in virtual environment
       "MCPClientScriptPath": "mcp_client.py",  // Path to the MCP client script
       "MCPServerAddress": "localhost:50051"  // Your MCP server address
     }
   }
   ```

3. Run the provider:
   ```bash
   dotnet run
   ```

4. The provider will:
   - Start the Python MCP client process
   - Connect to your MCP server
   - Register with Voxta
   - Begin handling action requests

5. Monitor the console output for:
   - Connection status
   - Action triggers
   - Error messages
   - MCP tool responses

## Troubleshooting

Common issues and solutions:

1. "MCPClientScriptPath is not configured"
   - Ensure `appsettings.json` exists and has the correct path to `mcp_client.py`

2. Python process fails to start
   - Verify Python virtual environment is activated
   - Check `PythonExePath` in configuration
   - Ensure `mcp-agent` is installed in the virtual environment

3. Cannot connect to MCP server
   - Verify the server is running
   - Check `MCPServerAddress` configuration
   - Ensure no firewall is blocking the connection

## Configuration

The provider is configured through `appsettings.json`:

```json
{
  "MCPBridge": {
    "PythonExePath": "venv/bin/python",  // On Windows: "venv\\Scripts\\python.exe"
    "MCPClientScriptPath": "mcp_client.py",  // Required: Path to the Python MCP client script
    "MCPServerAddress": "localhost:50051"  // Address of your MCP server
  },
  "Serilog": {
    "Using": ["Serilog.Sinks.Console"],
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:l}{NewLine}{Exception}",
          "theme": "Serilog.Sinks.SystemConsole.Themes.SystemConsoleTheme::Literate, Serilog.Sinks.Console"
        }
      }
    ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System.Net.Http.HttpClient": "Warning",
        "Voxta": "Information",
        "Voxta.MCPBridgeProvider": "Debug"
      }
    }
  }
}
```

### Required Configuration

- `MCPBridge:MCPClientScriptPath`: Path to the Python MCP client script that handles communication with the MCP server.
- `MCPBridge:MCPServerAddress`: Address of your MCP server (e.g., "localhost:50051" for a local server).

### Optional Configuration

- `MCPBridge:PythonExePath`: Path to the Python executable. Defaults to "python" (or "python3" on Linux).

## Connecting to MCP Servers

1. Start your MCP server (e.g., Home Assistant MCP server) and note its address (typically `localhost:50051`).

2. Update the `MCPServerAddress` in `appsettings.json` to match your MCP server's address.

3. The provider will automatically connect to the MCP server when started and make its tools available to Voxta.

## Usage

1. The provider will automatically register available MCP tools with Voxta.

2. When Voxta triggers an action, the provider will:
   - Translate the Voxta action to an MCP tool call
   - Send the request to the MCP server through the Python client
   - Receive the response and send it back to Voxta

## Development

- The C# code handles the Voxta integration and process management
- The Python script (`mcp_client.py`) handles MCP protocol communication
- Both components communicate through stdin/stdout using JSON messages

## License

[License information here]
