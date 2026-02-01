<div align="center">

# 🌿 Verdure MCP Server

**A comprehensive MCP (Model Context Protocol) server with IoT device integration**

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![MCP](https://img.shields.io/badge/MCP-Compatible-green.svg)](https://modelcontextprotocol.io/)

[Features](#-features) • [Quick Start](#-quick-start) • [Documentation](#-documentation) • [Architecture](#-architecture)

**[中文](README.md) | English**

</div>

---

## 📸 Screenshots

<div align="center">

### Web Dashboard
![Web Dashboard](assets/screenshots/home-page.jpg)
*Intuitive web interface for managing MCP tools, devices, and services*

### IoT Device Hardware
![ESP32 Device](assets/device-image.jpg)
*ESP32 device with speaker module - connects to Verdure MCP via SignalR*

</div>

---

## ✨ Features

### 🔧 **MCP Protocol Support**
- Full compliance with the [Model Context Protocol](https://modelcontextprotocol.io/)
- Multiple tool endpoints with category-based routing
- Streamable HTTP transport layer
- Bearer token authentication

### 🎨 **AI-Powered Tools**
- **Image Generation**: Azure OpenAI DALL-E integration with customizable parameters
- **Email Notifications**: Send generated content via SMTP
- **Music/Audio Playback**: Trigger audio playback on connected devices
- **Debug Tools**: Request inspection and header analysis

### 📡 **IoT Device Integration**
- **SignalR Device Hub**: Real-time bidirectional communication
- **ESP32 Support**: Optimized for ESP32/IoT device connectivity
- **Device Registry**: Track and manage connected devices
- **User-Device Binding**: Multi-user device sharing capabilities
- **Live Push Notifications**: Send commands and messages to devices

### ⚙️ **Production-Ready Infrastructure**
- **Async Processing**: Hangfire background job queue
- **PostgreSQL Storage**: Robust data persistence
- **Keycloak Integration**: Enterprise-grade authentication
- **Role-Based Access**: Fine-grained permission control
- **Docker Support**: Containerized deployment ready
- **Health Checks**: Built-in monitoring endpoints

---

## 📋 Table of Contents

- [Quick Start](#-quick-start)
- [Architecture](#-architecture)
- [MCP Tools](#-mcp-tools)
- [IoT Device Hub](#-iot-device-hub)
- [API Endpoints](#-api-endpoints)
- [Configuration](#-configuration)
- [Authentication](#-authentication)
- [Deployment](#-deployment)
- [Documentation](#-documentation)

---

## 🚀 Quick Start

### Prerequisites

- **.NET 10.0 SDK** or later
- **PostgreSQL** database
- **Azure OpenAI** resource with DALL-E deployment (optional, for image generation)
- **Keycloak** server (optional, for production authentication)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/maker-community/verdure-mcp.git
   cd verdure-mcp
   ```

2. **Set up PostgreSQL database**
   ```bash
   createdb verdure_mcp
   ```

3. **Configure application settings**
   
   Update [appsettings.json](src/Verdure.Mcp.Server/appsettings.json) or use user secrets:
   ```bash
   cd src/Verdure.Mcp.Server
   dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key"
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "your-connection-string"
   ```

4. **Run database migrations**
   ```bash
   dotnet ef database update
   ```

5. **Start the server**
   ```bash
   dotnet run
   ```

6. **Verify installation**
   - Web UI: `http://localhost:5000`
   - Health check: `http://localhost:5000/health`
   - MCP endpoint: `http://localhost:5000/mcp`
   - Hangfire dashboard: `http://localhost:5000/hangfire`

---

## 🏗️ Architecture

### Project Structure

```
src/
├── 🌐 Verdure.Mcp.Server/           # Main MCP Server application
│   ├── Tools/                        # MCP Tools implementation
│   │   ├── GenerateImageTool.cs      # 🎨 AI image generation
│   │   ├── EmailTool.cs              # 📧 Email notifications
│   │   ├── MusicTool.cs              # 🎵 Audio playback control
│   │   └── DebugTool.cs              # 🔍 Request debugging
│   ├── Hubs/                         # SignalR hubs
│   │   └── DeviceHub.cs              # 📡 IoT device communication
│   ├── Endpoints/                    # REST API endpoints
│   │   ├── DeviceEndpoints.cs        # Device management API
│   │   └── AdminEndpoints.cs         # Admin token management
│   ├── Services/                     # Business logic services
│   │   └── DevicePushServiceImpl.cs  # Device push service
│   └── Program.cs                    # Application entry point
│
├── 📦 Verdure.Mcp.Domain/            # Domain models and entities
│   ├── Entities/
│   │   ├── ImageGenerationTask.cs    # Image task entity
│   │   ├── ApiToken.cs               # Authentication tokens
│   │   ├── Device.cs                 # IoT device entity
│   │   ├── DeviceConnection.cs       # Active connections
│   │   └── DeviceBinding.cs          # Device sharing relationships
│   └── Enums/
│       ├── ImageTaskStatus.cs
│       ├── DeviceStatus.cs
│       └── DeviceBindingStatus.cs
│
├── 🔧 Verdure.Mcp.Infrastructure/    # Infrastructure services
│   ├── Data/
│   │   └── McpDbContext.cs           # EF Core DbContext
│   └── Services/
│       ├── ImageGenerationService.cs  # Azure OpenAI integration
│       ├── EmailService.cs            # MailKit email service
│       └── TokenValidationService.cs  # Token management
│
└── 🎨 Verdure.Mcp.Web/               # Blazor Web UI
    ├── Pages/                        # Web pages
    ├── Components/                   # Reusable components
    └── Services/                     # Frontend services
```

### Technology Stack

| Layer | Technologies |
|-------|-------------|
| **Backend** | ASP.NET Core 10.0, SignalR, Minimal APIs |
| **Frontend** | Blazor WebAssembly, Bootstrap 5 |
| **Database** | PostgreSQL, Entity Framework Core |
| **Authentication** | Keycloak, JWT Bearer Tokens |
| **Background Jobs** | Hangfire |
| **AI Services** | Azure OpenAI (DALL-E 3) |
| **Email** | MailKit/SMTP |
| **Containerization** | Docker, Docker Compose |

---

## 🛠️ MCP Tools

The server provides different sets of tools based on the endpoint you connect to. All MCP endpoints end with `/mcp` to identify them as Streamable HTTP protocol endpoints.

### 🎯 Available Endpoints

| Endpoint | Tools Available | Use Case |
|----------|----------------|----------|
| `/mcp` or `/all/mcp` | All tools | General-purpose MCP client |
| `/image/mcp` | Image generation only | AI image workflows |
| `/email/mcp` | Email tools only | Notification systems |
| `/music/mcp` | Music/audio tools | IoT audio control |
| `/debug/mcp` | Debug tools | Development & testing |

### 🔨 Tool Catalog

#### 1️⃣ **generate_image** - AI Image Generation

Generates high-quality images using Azure OpenAI DALL-E 3.

**Parameters:**
- `prompt` (required, string): Text description of the image to generate
- `size` (optional, string): Image dimensions
  - `1024x1024` - Square (default)
  - `1792x1024` - Landscape
  - `1024x1792` - Portrait
- `quality` (optional, string): 
  - `standard` - Faster, lower cost (default)
  - `hd` - Higher detail and quality
- `style` (optional, string):
  - `vivid` - Hyper-real, dramatic (default)
  - `natural` - More natural, less dramatic

**Headers:**
- `Authorization`: Bearer token (required)
- `X-User-Email`: Email to receive the generated image
- `X-User-Id`: Enable async processing

**Example:**
```json
{
  "name": "generate_image",
  "arguments": {
    "prompt": "A serene Japanese garden with cherry blossoms at sunset",
    "size": "1792x1024",
    "quality": "hd",
    "style": "natural"
  }
}
```

#### 2️⃣ **get_image_task_status** - Check Image Generation Status

Get the status of an asynchronous image generation task.

**Parameters:**
- `taskId` (required, string): The task ID returned by async image generation

**Response:**
```json
{
  "status": "Completed",
  "imageUrl": "https://...",
  "createdAt": "2026-02-01T10:30:00Z"
}
```

#### 3️⃣ **send_email** - Email Notifications

Send emails with optional image attachments.

**Parameters:**
- `toEmail` (required, string): Recipient email address
- `subject` (required, string): Email subject line
- `body` (required, string): Email body (HTML supported)
- `imageBase64` (optional, string): Base64-encoded image data
- `imageName` (optional, string): Attachment filename (default: `image.png`)

#### 4️⃣ **play_random_music** - Trigger Audio Playback

Push random audio playback command to connected IoT devices.

**Parameters:**
- `userId` (optional, string): Target user ID (broadcasts to their devices)

**Use case:** Trigger audio playback on ESP32 devices with speaker modules.

#### 5️⃣ **debug_headers** - Inspect Request Headers

Debug tool to view all HTTP headers received by the server.

**Response:** JSON object containing all request headers.

---

## 📡 IoT Device Hub

Verdure MCP includes a powerful SignalR hub for real-time communication with ESP32 and other IoT devices.

### Key Features

- ✅ **WebSocket-based** real-time bidirectional communication
- ✅ **Token-based authentication** via query string
- ✅ **User-device binding** for multi-user scenarios
- ✅ **Device status tracking** and heartbeat monitoring
- ✅ **Push notifications** from server to device
- ✅ **Device registration** with metadata support

### Quick Connection Example (ESP32)

```cpp
// Connect to SignalR hub with access token
String hubUrl = "wss://your-server.com/hub/device?access_token=YOUR_TOKEN";
hub_connection.start(hubUrl);

// Register device after connection
hub_connection.invoke("RegisterDevice", macAddress, deviceToken, metadataJson);

// Listen for server messages
hub_connection.on("CustomMessage", [](const char* message) {
    // Handle incoming commands from server
    Serial.println(message);
});
```

### Hub Endpoints

| Hub Route | Purpose | Authentication |
|-----------|---------|----------------|
| `/hub/device` | Device connection and management | Access token via query string |

### Hub Methods

**Client → Server:**
- `RegisterDevice(macAddress, deviceToken, metadata)` - Register/update device info
- `Heartbeat()` - Send keep-alive signal

**Server → Client:**
- `Notification` - Connection confirmation
- `CustomMessage` - Push commands/data to device
- `DeviceCommand` - Structured device control commands

### Device Management API

REST API for managing devices (requires authentication):

```bash
# Get all devices for current user
GET /api/devices

# Get specific device
GET /api/devices/{deviceId}

# Get active connections
GET /api/devices/connections

# Push message to user's devices
POST /api/devices/push
{
  "userId": "user-123",
  "message": "Hello Device!"
}

# Push to specific device
POST /api/devices/{deviceId}/push
{
  "message": "Device-specific command"
}
```

📖 **Full documentation:** [SignalR Device Hub Documentation](docs/SIGNALR_DEVICE_HUB.md)

---

## 🔐 Authentication

### Token-Based Authentication

The server uses **Bearer token authentication** for API and MCP access. Tokens are stored securely using PBKDF2 hashing with 100,000 iterations.

#### Development Mode (Token Creation)

Create tokens using the admin endpoint (development only):

```bash
# Create a new API token
curl -X POST "http://localhost:5000/admin/tokens?name=my-app-token"

# Response:
{
  "token": "vtk_abc123def456...",
  "name": "my-app-token",
  "createdAt": "2026-02-01T10:00:00Z"
}
```

⚠️ **Security Note:** The admin token endpoint is only available in development mode.

#### Using Tokens

**MCP Client (Claude Desktop):**
```json
{
  "mcpServers": {
    "verdure": {
      "transport": {
        "type": "http",
        "url": "http://localhost:5000/mcp",
        "headers": {
          "Authorization": "Bearer vtk_your_token_here"
        }
      }
    }
  }
}
```

**REST API:**
```bash
curl -H "Authorization: Bearer vtk_your_token_here" \
     http://localhost:5000/api/devices
```

**SignalR (ESP32):**
```cpp
// Token passed as query parameter
String url = "ws://server.com/hub/device?access_token=vtk_your_token_here";
```

### Keycloak Integration (Production)

For production deployments, integrate with Keycloak for enterprise SSO:

```json
{
  "Authentication": {
    "Schemes": {
      "Keycloak": {
        "Authority": "https://keycloak.example.com/realms/your-realm",
        "Audience": "verdure-mcp",
        "ValidateAudience": true,
        "RequireHttpsMetadata": true
      }
    }
  }
}
```

---

## ⚙️ Configuration

### appsettings.json Structure

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=verdure_mcp;Username=postgres;Password=yourpassword"
  },
  
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-api-key",
    "DeploymentName": "dall-e-3",
    "ApiVersion": "2024-02-01"
  },
  
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "your-app-password",
    "UseSsl": true,
    "FromEmail": "noreply@example.com",
    "FromName": "Verdure MCP"
  },
  
  "Authentication": {
    "RequireToken": true,
    "TokenPrefix": "vtk_"
  },
  
  "Hangfire": {
    "DashboardPath": "/hangfire",
    "WorkerCount": 5
  }
}
```

### Environment Variables (Recommended for Production)

```bash
# Database
ConnectionStrings__DefaultConnection="Host=postgres;Database=verdure_mcp;..."

# Azure OpenAI
AzureOpenAI__ApiKey="your-secure-api-key"
AzureOpenAI__Endpoint="https://your-resource.openai.azure.com/"

# Email
Email__SmtpPassword="your-secure-smtp-password"

# Authentication
Authentication__RequireToken="true"
```

---

## 🚢 Deployment

### Docker Deployment

#### Option 1: Docker Compose (Recommended)

```bash
# Build and start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop services
docker-compose down
```

The compose file includes:
- Verdure MCP Server
- PostgreSQL database
- Keycloak (optional)

#### Option 2: Standalone Docker

```bash
# Build image
docker build -t verdure-mcp:latest -f docker/Dockerfile .

# Run container
docker run -d \
  -p 5000:8080 \
  -e ConnectionStrings__DefaultConnection="Host=postgres;..." \
  -e AzureOpenAI__ApiKey="your-key" \
  --name verdure-mcp \
  verdure-mcp:latest
```

### Production Checklist

- [ ] Use HTTPS for all communications
- [ ] Store secrets in environment variables or Azure Key Vault
- [ ] Enable `Authentication:RequireToken`
- [ ] Configure Keycloak or other identity provider
- [ ] Set up database backups
- [ ] Configure logging and monitoring
- [ ] Use reverse proxy (nginx/Caddy) for SSL termination
- [ ] Limit Hangfire dashboard access
- [ ] Review CORS settings

---

## 🎓 MCP Client Configuration

### Claude Desktop Configuration

To use Verdure MCP with Claude Desktop, add to your MCP configuration file:

**Windows:** `%APPDATA%\Claude\claude_desktop_config.json`  
**macOS:** `~/Library/Application Support/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "verdure-all-tools": {
      "transport": {
        "type": "http",
        "url": "http://localhost:5000/mcp",
        "headers": {
          "Authorization": "Bearer vtk_your_token_here"
        }
      }
    },
    "verdure-image-only": {
      "transport": {
        "type": "http",
        "url": "http://localhost:5000/image/mcp",
        "headers": {
          "Authorization": "Bearer vtk_your_token_here"
        }
      }
    }
  }
}
```

### Custom MCP Client

```python
# Python example
import anthropic

client = anthropic.Anthropic(api_key="your-api-key")

response = client.messages.create(
    model="claude-3-5-sonnet-20241022",
    max_tokens=1024,
    mcp_servers=[
        {
            "type": "http",
            "url": "http://localhost:5000/mcp",
            "headers": {
                "Authorization": "Bearer vtk_your_token_here"
            }
        }
    ],
    messages=[
        {
            "role": "user",
            "content": "Generate an image of a futuristic city"
        }
    ]
)
```

---

## 📚 Documentation

| Document | Description |
|----------|-------------|
| [SignalR Device Hub](docs/SIGNALR_DEVICE_HUB.md) | Complete guide for IoT device integration |
| [Role Implementation](docs/ROLE_IMPLEMENTATION_SUMMARY.md) | Role-based access control setup |
| [Testing Guide](docs/TESTING.md) | Testing strategies and examples |
| [Tool Categories](docs/TOOL_CATEGORY_GUIDE.md) | Organizing and categorizing MCP tools |
| [Docker Deployment](docker/README.md) | Containerization details |

---

## 🔄 Async Processing

When the `X-User-Id` header is present in requests, **image generation tasks** are processed asynchronously using Hangfire.

### Benefits
- ✅ Non-blocking API responses
- ✅ Retry logic for failed tasks
- ✅ Progress tracking via task ID
- ✅ Background job monitoring (Hangfire dashboard)

### Usage Example

```bash
# Trigger async image generation
curl -X POST http://localhost:5000/mcp \
  -H "Authorization: Bearer vtk_token" \
  -H "X-User-Id: user123" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "generate_image",
    "arguments": {
      "prompt": "A beautiful landscape"
    }
  }'

# Response:
{
  "taskId": "abc-123-def",
  "status": "Pending"
}

# Check status later
curl -X POST http://localhost:5000/mcp \
  -H "Authorization: Bearer vtk_token" \
  -d '{
    "name": "get_image_task_status",
    "arguments": {
      "taskId": "abc-123-def"
    }
  }'
```

---

## 🔒 Security Features

### Token Security
- **PBKDF2 Hashing**: 100,000 iterations with random salt
- **Constant-time comparison**: Prevents timing attacks
- **Prefix validation**: `vtk_` prefix for easy identification

### Input Validation
- **HTML encoding**: Prevents XSS in email content
- **SQL parameterization**: Prevents SQL injection
- **Schema validation**: All MCP inputs validated against JSON schemas

### Network Security
- **CORS configuration**: Restrictive CORS policies
- **HTTPS enforcement**: Production mode requires HTTPS
- **Rate limiting**: Configurable request throttling (Hangfire)

---

## 🤝 Contributing

Contributions are welcome! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## � Community & Support

### Join our community for help and sharing!

<div align="center">

| 💬 **QQ Group** | 📺 **Bilibili** | 🐙 **GitHub Community** |
|:---:|:---:|:---:|
| **Greenery DIY Hardware Group** | **绿荫阿广 Verdure Hiro** | **Maker Community** |
| Group: **1023487000** | [Visit Channel](https://space.bilibili.com/25228512) | [Visit Org](https://github.com/maker-community) |
| Discuss AI, MCP and Hardware DIY | Get AI & Maker Tutorials | Contribute Code, Join Development |

</div>

### 📮 Contact

- **📧 Issue Reports**: [GitHub Issues](https://github.com/maker-community/verdure-mcp/issues)
- **💡 Feature Requests**: [GitHub Discussions](https://github.com/maker-community/verdure-mcp/discussions)
- **🎥 Video Tutorials**: [Bilibili @绿荫阿广 Verdure Hiro](https://space.bilibili.com/25228512)
- **🔗 XiaoZhi Bridge Platform**: [Verdure MCP for XiaoZhi](https://github.com/maker-community/verdure-mcp-for-xiaozhi) - MCP bridge service designed for XiaoZhi AI Assistant, with online platform and multi-tenant SaaS solution

---

## 🌟 Acknowledgments

Thanks to the following open source projects and technologies:

- [Model Context Protocol](https://modelcontextprotocol.io/) - Protocol specification
- [Azure OpenAI](https://azure.microsoft.com/en-us/products/ai-services/openai-service) - AI services
- [Anthropic Claude](https://www.anthropic.com/) - MCP client reference
- [ESP32 Community](https://github.com/maker-community) - IoT device support
- [Microsoft .NET](https://dotnet.microsoft.com/) - Powerful cross-platform development framework
- [Keycloak](https://www.keycloak.org/) - Open source identity and access management solution
- [Entity Framework Core](https://docs.microsoft.com/ef/core/) - Modern ORM framework

Special thanks to all developers and makers who support and use this project!

⭐ **If this project helps you, please give us a Star!** ⭐

---

<div align="center">

**Made with ❤️ by [绿荫阿广 Verdure Hiro](https://space.bilibili.com/25228512) and the [Maker Community](https://github.com/maker-community)**

[Report Bug](https://github.com/maker-community/verdure-mcp/issues) • [Request Feature](https://github.com/maker-community/verdure-mcp/issues) • [🏠 Back to Top](#-verdure-mcp-server)

</div>
