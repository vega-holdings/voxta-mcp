import asyncio
import json
import os
from pathlib import Path

from mcp_agent.app import MCPApp
from mcp_agent.mcp.mcp_aggregator import MCPAggregator

app = MCPApp(name="voxta_tool_exporter")

async def export_tools():
    """Export tools to JSON file"""
    try:
        # Create an MCP aggregator for all configured servers
        aggregator = await MCPAggregator.create(
            server_names=["notion", "filesystem"],
            connection_persistence=True
        )
        
        tool_list = []
        try:
            # Get tools list from all servers
            tools_response = await aggregator.list_tools()
            print(f"Tools response type: {type(tools_response)}")
            print(f"Tools response content: {tools_response}")
            
            # Convert tools to JSON-serializable format
            if hasattr(tools_response, 'tools'):
                tools_to_process = tools_response.tools
            else:
                print("Warning: tools_response does not have 'tools' attribute")
                tools_to_process = []

            for tool in tools_to_process:
                print(f"Processing tool: {tool}")
                if hasattr(tool, 'name'):
                    # Extract the tool data
                    tool_info = {
                        "name": tool.name,
                        "description": tool.description or "",
                        "parameters": tool.inputSchema or {}
                    }
                    print(f"Added tool info: {tool_info}")
                    tool_list.append(tool_info)
                else:
                    print(f"Skipping tool without name attribute: {tool}")
            
            print(f"Total tools processed: {len(tool_list)}")
        finally:
            # Always close the aggregator
            await aggregator.close()
        
        if not tool_list:
            print("Warning: No tools were collected!")
            return
            
        # Create JSON structure
        tools_json = {
            "tools": tool_list
        }
        
        print(f"Final JSON structure: {json.dumps(tools_json, indent=2)}")
        
        # Write to file
        output_path = Path(__file__).parent / "mcp_tools.json"
        try:
            # Ensure the parent directory exists
            output_path.parent.mkdir(parents=True, exist_ok=True)
            
            # Write the file with explicit flush
            with open(output_path, "w", encoding="utf-8") as f:
                json.dump(tools_json, f, indent=2, ensure_ascii=False)
                f.flush()
                os.fsync(f.fileno())  # Ensure it's written to disk
            
            print(f"File written to {output_path}")
            
            # Verify the file exists and has content
            if output_path.exists() and output_path.stat().st_size > 0:
                # Read back the file to verify its contents
                with open(output_path, "r", encoding="utf-8") as f:
                    content = json.load(f)
                    if content and "tools" in content and len(content["tools"]) > 0:
                        print(f"Tools successfully exported to {output_path}")
                        print(f"Exported {len(content['tools'])} tools")
                    else:
                        raise Exception("File was created but contains no tools")
            else:
                raise Exception("File was not created or is empty")
        except Exception as e:
            print(f"Error writing file: {str(e)}")
            raise
            
    except Exception as e:
        print(f"Error exporting tools: {str(e)}")
        raise

async def main():
    async with app.run():
        print("MCP Agent started")
        await export_tools()

if __name__ == "__main__":
    asyncio.run(main())
