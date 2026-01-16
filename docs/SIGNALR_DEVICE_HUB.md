# SignalR Device Hub Documentation

## Overview

The Verdure MCP server includes a SignalR hub that enables real-time communication with ESP32 and other IoT devices. Devices can connect using an access token, register themselves, and receive push notifications and commands from the server.

## Architecture

### Domain Models

- **Device**: Represents an IoT device (e.g., ESP32) with MAC address, owner, status, and metadata
- **DeviceConnection**: Tracks active SignalR connections from devices
- **DeviceBinding**: Manages device sharing relationships between users (for future social features)

### Hub Endpoint

- **Route**: `/hub/device`
- **Authentication**: Access token via query string parameter `?access_token=YOUR_TOKEN`

## Device Connection Flow

### 1. Connect to Hub

Devices connect to the SignalR hub using an access token for authentication:

```
wss://your-server.com/hub/device?access_token=YOUR_ACCESS_TOKEN
```

The hub will:
1. Validate the access token
2. Extract the userId from the token
3. Add the connection to a user group `Users:{userId}`
4. Send a welcome `Notification` to the device

### 2. Register Device

After connecting, devices should call the `RegisterDevice` method to register or update their information:

```javascript
// JavaScript/TypeScript example
await connection.invoke("RegisterDevice", macAddress, deviceToken, metadata);

// C++ ESP32 example (pseudo-code)
hub.invoke("RegisterDevice", macAddress, deviceToken, metadataJson);
```

**Parameters:**
- `macAddress` (string, required): The device's MAC address (unique identifier)
- `deviceToken` (string, optional): Device-specific token for additional authentication
- `metadata` (string, optional): JSON string with device information (firmware version, type, etc.)

**Response:**
The device will receive a `DeviceRegistered` event with:
```json
{
  "deviceId": "guid",
  "macAddress": "AA:BB:CC:DD:EE:FF",
  "status": "Online",
  "timestamp": "2024-01-16T12:00:00Z"
}
```

### 3. Receive Messages

Devices can listen for the following events:

#### Notification
General notifications from the server:
```json
{
  "message": "Connected to Verdure MCP Device Hub",
  "timestamp": "2024-01-16T12:00:00Z",
  "connectionId": "connection-id"
}
```

#### CustomMessage
Custom commands or data from the server:
```json
{
  // Custom payload defined by application
}
```

### 4. Heartbeat (Optional)

Devices can send periodic heartbeats to update their last-seen timestamp:

```javascript
await connection.invoke("Heartbeat");
```

### 5. Disconnect

When a device disconnects:
- The connection record is removed from the database
- The device status is updated to `Offline`
- The last seen timestamp is updated

## Server-to-Device Push

### Using the Device Push Service

Server-side code can push messages to devices using the `IDevicePushService`:

```csharp
// Inject the service
private readonly IDevicePushService _pushService;

// Send to all devices owned by a user
await _pushService.SendToUserAsync(userId, "CustomMessage", new { 
    command = "update_settings",
    data = settings 
});

// Send to a specific device
await _pushService.SendToDeviceAsync(deviceId, "CustomMessage", new {
    command = "restart"
});

// Send a notification
await _pushService.SendNotificationAsync(userId, "Your device is online");

// Send a custom message (uses CustomMessage method)
await _pushService.SendCustomMessageAsync(userId, new {
    type = "alert",
    message = "Temperature threshold exceeded"
});
```

## REST API Endpoints

### Get User's Devices
```http
GET /api/devices
Authorization: Bearer {jwt_token}
```

Response:
```json
{
  "success": true,
  "data": [
    {
      "id": "guid",
      "macAddress": "AA:BB:CC:DD:EE:FF",
      "status": "Online",
      "lastSeenAt": "2024-01-16T12:00:00Z",
      "metadata": "{\"firmware\":\"1.0.0\",\"type\":\"ESP32\"}",
      "createdAt": "2024-01-15T10:00:00Z",
      "updatedAt": "2024-01-16T12:00:00Z"
    }
  ]
}
```

### Get Specific Device
```http
GET /api/devices/{deviceId}
Authorization: Bearer {jwt_token}
```

### Get Device Connections
```http
GET /api/devices/{deviceId}/connections
Authorization: Bearer {jwt_token}
```

### Send Message to Device
```http
POST /api/devices/{deviceId}/send
Authorization: Bearer {jwt_token}
Content-Type: application/json

{
  "method": "CustomMessage",
  "payload": {
    "command": "update_settings",
    "settings": { "brightness": 80 }
  }
}
```

### Delete Device
```http
DELETE /api/devices/{deviceId}
Authorization: Bearer {jwt_token}
```

## ESP32 Integration Example

Based on the reference implementation at https://github.com/maker-community/xiaozhi-esp32/blob/signalr/main/signalr_client.cc

### Connection Setup

```cpp
// 1. Connect to WiFi
WiFi.begin(ssid, password);

// 2. Connect to SignalR Hub
String hubUrl = "wss://your-server.com/hub/device";
String accessToken = "YOUR_ACCESS_TOKEN";
String fullUrl = hubUrl + "?access_token=" + accessToken;

// Initialize SignalR client with the URL
signalRClient.begin(fullUrl);

// 3. Set up event handlers
signalRClient.on("Notification", [](const JsonObject& message) {
    String msg = message["message"].as<String>();
    Serial.println("Notification: " + msg);
});

signalRClient.on("CustomMessage", [](const JsonObject& message) {
    // Handle custom messages/commands
    String command = message["command"].as<String>();
    handleCommand(command, message);
});

signalRClient.on("DeviceRegistered", [](const JsonObject& message) {
    String deviceId = message["deviceId"].as<String>();
    Serial.println("Device registered with ID: " + deviceId);
});

// 4. Register the device after connection
void onConnected() {
    String macAddress = WiFi.macAddress();
    String metadata = "{\"firmware\":\"1.0.0\",\"type\":\"ESP32\"}";
    
    signalRClient.invoke("RegisterDevice", macAddress, "", metadata);
}
```

### Periodic Heartbeat

```cpp
unsigned long lastHeartbeat = 0;
const unsigned long heartbeatInterval = 30000; // 30 seconds

void loop() {
    signalRClient.loop();
    
    unsigned long now = millis();
    if (now - lastHeartbeat > heartbeatInterval) {
        signalRClient.invoke("Heartbeat");
        lastHeartbeat = now;
    }
}
```

## Token Management

### Create Access Token

Users can create access tokens via the REST API:

```http
POST /api/tokens
Authorization: Bearer {jwt_token}
Content-Type: application/json

{
  "name": "My ESP32 Device"
}
```

Response:
```json
{
  "success": true,
  "data": {
    "id": "guid",
    "token": "base64-encoded-token",
    "name": "My ESP32 Device",
    "expiresAt": "2024-02-15T10:00:00Z",
    "message": "请妥善保管此密钥，它只会显示一次。"
  }
}
```

**Important**: Store the token securely. It will not be shown again.

### List User Tokens

```http
GET /api/tokens
Authorization: Bearer {jwt_token}
```

### Revoke Token

```http
DELETE /api/tokens/{tokenId}
Authorization: Bearer {jwt_token}
```

## Security Considerations

1. **Token Storage**: Access tokens are hashed using PBKDF2 with 100,000 iterations before storage
2. **HTTPS Required**: Always use HTTPS/WSS in production to protect tokens in transit
3. **Token Expiration**: Tokens have a default expiration period (configurable)
4. **Device Ownership**: API endpoints verify device ownership before allowing operations
5. **Connection Validation**: Each connection is authenticated via access token

## Database Schema

### devices
- `id` (uuid): Primary key
- `mac_address` (varchar): Unique device identifier
- `owner_user_id` (varchar): Owner's user ID
- `last_seen_at` (timestamp): Last connection/heartbeat time
- `status` (int): Device status (Offline=0, Online=1, Registered=2)
- `metadata` (jsonb): Device metadata (firmware, type, etc.)
- `created_at` (timestamp): Registration time
- `updated_at` (timestamp): Last update time

### device_connections
- `connection_id` (varchar): SignalR connection ID (primary key)
- `device_id` (uuid): Associated device
- `user_id` (varchar): Connection owner
- `connected_at` (timestamp): Connection time
- `last_heartbeat_at` (timestamp): Last heartbeat

### device_bindings
- `id` (uuid): Primary key
- `owner_user_id` (varchar): Device owner
- `target_user_id` (varchar): User with access
- `device_id` (uuid): Bound device
- `status` (int): Binding status (Pending=0, Active=1, Rejected=2, Revoked=3)
- `created_at` (timestamp): Binding creation time
- `updated_at` (timestamp): Status update time

## Future Enhancements

The device binding system provides a foundation for "social" features:

1. **Device Sharing**: Users can share device access with others
2. **Approval Flow**: Implement pending/approval workflow for bindings
3. **Permission Levels**: Add different access levels (view-only, control, admin)
4. **Device Groups**: Group devices for batch operations
5. **Event History**: Track device events and commands

## Troubleshooting

### Device Cannot Connect

1. Verify the access token is valid and not expired
2. Check that the token is URL-encoded in the query string
3. Ensure the server is reachable and HTTPS/WSS is configured correctly
4. Check server logs for authentication errors

### Device Not Receiving Messages

1. Verify the device called `RegisterDevice` after connecting
2. Check that the device is in the correct user group
3. Verify the userId in the access token matches the target user
4. Check SignalR connection status

### Devices Show as Offline

1. Ensure devices send periodic heartbeats
2. Check that `OnDisconnectedAsync` is properly cleaning up connections
3. Verify database timestamps are in UTC
4. Check for network interruptions

## References

- ESP32 Client Implementation: https://github.com/maker-community/xiaozhi-esp32/blob/signalr/main/signalr_client.cc
- SignalR Server Example: https://github.com/maker-community/esp-signalr-example/tree/main/signalr-server
- ASP.NET Core SignalR Documentation: https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction
