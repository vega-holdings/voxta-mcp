import asyncio
import json
import sys
import os

from mcp_agent.app import MCPApp
from mcp_agent.agents.agent import Agent

app = MCPApp(name="voxta_bridge_agent")
agent = Agent(name="notion_agent")

async def main():
    async with app.run():
        print("MCP Agent started.", file=sys.stderr)
        async with agent:
            # Removed agent.startup.wait() because 'startup' isn't available in this version.
            print(json.dumps({"status": "ready"}), flush=True)
            
            # Load command from mcp_tools.json instead of stdin.
            try:
                with open("mcp_tools.json", "r") as f:
                    request = json.load(f)
            except Exception as e:
                print(json.dumps({"error": f"Failed to load mcp_tools.json: {str(e)}"}), flush=True)
                return

            method = request.get("method")
            if method == "quit":
                print(json.dumps({"result": "finished"}), flush=True)
            elif method == "call_tool":
                try:
                    params = request.get("params", {})
                    result = await agent.call_tool(
                        name=params.get("name"),
                        arguments=params.get("arguments", {})
                    )
                    print(json.dumps({"result": result}), flush=True)
                except Exception as e:
                    print(json.dumps({"error": str(e)}), flush=True)
            else:
                # Echo back any other messages
                print(json.dumps(request), flush=True)

if __name__ == "__main__":
    asyncio.run(main())
