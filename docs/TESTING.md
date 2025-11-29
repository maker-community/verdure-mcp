# Testing Guide for Verdure MCP Server

This guide explains how to test the multi-endpoint functionality of the Verdure MCP Server.

## Prerequisites

1. Server is running: `dotnet run --project src/Verdure.Mcp.Server`
2. You have created an API token (in development mode)
3. Configure Azure OpenAI and Email settings in `appsettings.json`

## Creating a Test Token

In development mode, create a token using:

```bash
curl -X POST "http://localhost:5000/admin/tokens?name=test-token"
```

Save the returned token for use in subsequent requests.

## Testing Endpoints

### 1. Test Health Endpoint (No Auth Required)

```bash
curl http://localhost:5000/health
```

Expected response:
```json
{
  "status": "healthy",
  "timestamp": "2025-11-29T...",
  "version": "1.0.0"
}
```

### 2. Test All Tools Endpoint

List all available tools:

```bash
curl -X POST http://localhost:5000/all \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "tools/list",
    "params": {}
  }'
```

Expected: You should see both `generate_image`, `get_image_task_status`, and `send_email` tools.

### 3. Test Image-Only Endpoint

```bash
curl -X POST http://localhost:5000/image \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "tools/list",
    "params": {}
  }'
```

Expected: You should see only `generate_image` and `get_image_task_status` tools.

### 4. Test Email-Only Endpoint

```bash
curl -X POST http://localhost:5000/email \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "tools/list",
    "params": {}
  }'
```

Expected: You should see only `send_email` tool.

## Testing Tool Invocation

### Generate Image (Sync Mode)

```bash
curl -X POST http://localhost:5000/image \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/call",
    "params": {
      "name": "generate_image",
      "arguments": {
        "prompt": "A beautiful sunset over mountains"
      }
    }
  }'
```

### Generate Image (Async Mode with Email)

```bash
curl -X POST http://localhost:5000/image \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "X-User-Id: user123" \
  -H "X-User-Email: test@example.com" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/call",
    "params": {
      "name": "generate_image",
      "arguments": {
        "prompt": "A futuristic city skyline",
        "size": "1024x1024",
        "quality": "standard"
      }
    }
  }'
```

### Send Email

```bash
curl -X POST http://localhost:5000/email \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 3,
    "method": "tools/call",
    "params": {
      "name": "send_email",
      "arguments": {
        "toEmail": "recipient@example.com",
        "subject": "Test Email",
        "body": "This is a test email from Verdure MCP Server"
      }
    }
  }'
```

## Testing with MCP Clients

### Claude Desktop Configuration

Add to your Claude Desktop configuration file:

**Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
**macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "verdure-all": {
      "transport": {
        "type": "http",
        "url": "http://localhost:5000/all",
        "headers": {
          "Authorization": "Bearer YOUR_TOKEN_HERE"
        }
      }
    },
    "verdure-image": {
      "transport": {
        "type": "http",
        "url": "http://localhost:5000/image",
        "headers": {
          "Authorization": "Bearer YOUR_TOKEN_HERE"
        }
      }
    },
    "verdure-email": {
      "transport": {
        "type": "http",
        "url": "http://localhost:5000/email",
        "headers": {
          "Authorization": "Bearer YOUR_TOKEN_HERE"
        }
      }
    }
  }
}
```

### Testing with Python MCP Client

```python
import asyncio
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

async def test_mcp_server():
    # Connect to the MCP server
    async with stdio_client(StdioServerParameters(
        command="curl",
        args=[
            "-X", "POST",
            "http://localhost:5000/all",
            "-H", "Authorization: Bearer YOUR_TOKEN",
            "-H", "Content-Type: application/json"
        ]
    )) as (read, write):
        async with ClientSession(read, write) as session:
            # List available tools
            tools = await session.list_tools()
            print(f"Available tools: {[t.name for t in tools]}")
            
            # Call a tool
            result = await session.call_tool("generate_image", {
                "prompt": "A serene lake at dawn"
            })
            print(f"Result: {result}")

if __name__ == "__main__":
    asyncio.run(test_mcp_server())
```

## Verification Checklist

- [ ] Health endpoint returns healthy status
- [ ] `/all` endpoint exposes all tools
- [ ] `/image` endpoint exposes only image-related tools
- [ ] `/email` endpoint exposes only email tool
- [ ] Image generation works in sync mode
- [ ] Image generation works in async mode with background job
- [ ] Email sending works correctly
- [ ] Bearer token authentication works
- [ ] Unauthorized requests are rejected

## Common Issues

### 404 Errors

- Make sure you're using POST requests for MCP protocol
- Verify the endpoint path (`/all`, `/image`, or `/email`)
- Check that the server is running on the correct port

### Authentication Errors

- Verify the Bearer token is correct
- Check that authentication is configured in `appsettings.json`
- In development, you can disable authentication by setting `Authentication:RequireToken` to `false`

### Image Generation Failures

- Verify Azure OpenAI credentials are correctly configured
- Check that the deployment name matches your Azure OpenAI deployment
- Review server logs for detailed error messages

### Email Failures

- Verify SMTP settings in `appsettings.json`
- Test SMTP connectivity independently
- Check email logs in the server output

## Monitoring

### View Hangfire Dashboard (Development Only)

```
http://localhost:5000/hangfire
```

Monitor background jobs, including async image generation tasks.

### Check Logs

The server logs all operations, including:
- Tool filtering by endpoint
- Image generation requests
- Email sending attempts
- Authentication attempts

Look for lines like:
```
MCP session starting for tool category: image
Filtered to 2 image tools
```
