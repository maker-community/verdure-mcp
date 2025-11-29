# Verdure MCP Server

A comprehensive MCP (Model Context Protocol) server that provides multiple tools and capabilities through a unified service. This server implements the [MCP specification](https://modelcontextprotocol.io/) and supports extensible tool sets with different functionalities.

## Features

- **Extensible Tool System**: Modular architecture supporting multiple MCP tools
- **Image Generation**: Generate images using Azure OpenAI DALL-E with customizable size, quality, and style
- **Email Notifications**: Optionally send generated content to email addresses
- **Async Processing**: Background job processing with Hangfire for long-running tasks
- **Bearer Token Authentication**: Secure API access with token-based authentication
- **PostgreSQL Storage**: Persist tasks and API tokens
- **MCP Protocol Support**: Full compliance with the Model Context Protocol

## Project Structure

```
src/
├── Verdure.Mcp.Server/               # Main MCP Server application
│   ├── Tools/                        # MCP Tools implementation
│   │   ├── GenerateImageTool.cs      # Image generation tool
│   │   └── ImageGenerationBackgroundJob.cs
│   ├── Filters/                      # Custom middleware
│   │   └── BearerTokenAuthenticationMiddleware.cs
│   ├── Settings/                     # Configuration settings
│   └── Program.cs                    # Application entry point
├── Verdure.Mcp.Domain/               # Domain models and entities
│   ├── Entities/
│   │   ├── ImageGenerationTask.cs
│   │   └── ApiToken.cs
│   └── Enums/
│       └── ImageTaskStatus.cs
└── Verdure.Mcp.Infrastructure/       # Infrastructure services
    ├── Data/
    │   └── McpDbContext.cs           # EF Core DbContext
    └── Services/
        ├── ImageGenerationService.cs  # Azure OpenAI integration
        ├── EmailService.cs            # MailKit email service
        └── TokenValidationService.cs  # API token management
```

## Prerequisites

- .NET 9.0 SDK or later
- PostgreSQL database
- Azure OpenAI resource with DALL-E deployment (for image generation)

## Configuration

Configure the application using `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=verdure_mcp;Username=postgres;Password=postgres"
  },
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-api-key-here",
    "DeploymentName": "dall-e-3",
    "ApiVersion": "2024-02-01"
  },
  "Email": {
    "SmtpHost": "smtp.example.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-username",
    "SmtpPassword": "your-password",
    "UseSsl": true,
    "FromEmail": "noreply@example.com",
    "FromName": "Verdure MCP"
  },
  "Authentication": {
    "RequireToken": true
  }
}
```

## Getting Started

1. **Clone the repository**
   ```bash
   git clone https://github.com/maker-community/verdure-mcp.git
   cd verdure-mcp
   ```

2. **Set up the database**
   ```bash
   # Create PostgreSQL database
   createdb verdure_mcp
   ```

3. **Configure Azure OpenAI credentials**
   
   Update `appsettings.json` or use user secrets:
   ```bash
   dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key"
   dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
   ```

4. **Run the application**
   ```bash
   cd src/Verdure.Mcp.Server
   dotnet run
   ```

5. **Access the server**
   - MCP endpoint: `http://localhost:5000/mcp`
   - Health check: `http://localhost:5000/health`
   - Hangfire dashboard (dev only): `http://localhost:5000/hangfire`

## MCP Tools

### generate_image

Generates an image based on a text prompt using Azure OpenAI DALL-E.

**Parameters:**
- `prompt` (required): The text prompt describing the image to generate
- `size` (optional): Image size - "1024x1024", "1792x1024", or "1024x1792" (default: "1024x1024")
- `quality` (optional): Image quality - "standard" or "hd" (default: "standard")
- `style` (optional): Image style - "vivid" or "natural" (default: "vivid")

**Request Headers:**
- `Authorization`: Bearer token for authentication
- `X-User-Email` (optional): Email address to receive the generated image
- `X-User-Id` (optional): If present, the task runs asynchronously

### get_image_task_status

Gets the status of an image generation task.

**Parameters:**
- `taskId` (required): The ID of the task to check

## Authentication

The server uses Bearer token authentication. Tokens are stored securely (hashed) in the database.

To create a new token (in development mode with authentication disabled):
```bash
curl -X POST "http://localhost:5000/admin/tokens?name=my-token"
```

Use the token in requests:
```bash
curl -H "Authorization: Bearer <your-token>" http://localhost:5000/mcp
```

## Async Processing

When the `X-User-Id` header is present in requests, image generation tasks are processed asynchronously using Hangfire. This prevents blocking the request while waiting for the image to be generated.

The task status can be checked using the `get_image_task_status` tool with the returned task ID.

## Security Notes

- **Token Storage**: API tokens are hashed using PBKDF2 with 100,000 iterations, random salt, and constant-time comparison for maximum security.
- **Configuration**: In production, use environment variables or user secrets instead of `appsettings.json` for sensitive data like API keys and connection strings.
- **Email Content**: User prompts are HTML-encoded before being included in email content to prevent XSS attacks.
- **Admin Endpoints**: Token creation endpoint is only available in development environment.

## Production Deployment

For production deployments:

1. Use environment variables or Azure Key Vault for secrets:
   ```bash
   export ConnectionStrings__DefaultConnection="your-secure-connection-string"
   export AzureOpenAI__ApiKey="your-api-key"
   export Email__SmtpPassword="your-smtp-password"
   ```

2. Enable authentication by setting `Authentication:RequireToken` to `true`.

3. Use HTTPS for all communications.

4. Consider implementing token caching for high-throughput scenarios.

## License

MIT License - see [LICENSE](LICENSE) for details.

