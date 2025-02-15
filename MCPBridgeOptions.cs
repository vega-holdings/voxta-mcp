using System.ComponentModel.DataAnnotations;

namespace Voxta.VoxtaMCPBridge;

public class MCPBridgeOptions
{
    [Required(ErrorMessage = "MCPClientScriptPath is required")]
    public string MCPClientScriptPath { get; set; } = string.Empty;
    
    public string PythonExePath { get; set; } = "python";
    
    [Required(ErrorMessage = "MCPServerAddress is required")]
    public string MCPServerAddress { get; set; } = "localhost:50051";
}
