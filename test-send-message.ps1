# Test script to send messages to ESP32 device via SignalR Hub
# Run this in PowerShell

# Your user ID from the JWT token
$userId = "0c8a5beb-2549-4126-8fdb-2052274b3796"

Write-Host "Testing SignalR Device Communication" -ForegroundColor Cyan
Write-Host ""

# Test 1: Send notification (xiaozhi format)
Write-Host "Test 1: Sending notification message to UserId: $userId" -ForegroundColor Yellow
$message1 = @{
    action = "notification"
    title = "System Alert"
    content = "You have a new message"
    emotion = "bell"
    sound = "popup"
}

$response1 = Invoke-RestMethod -Uri "http://localhost:5000/admin/device/send-message" `
    -Method POST `
    -ContentType "application/json" `
    -Body (@{
        userId = $userId
        message = $message1
    } | ConvertTo-Json -Depth 10)

Write-Host "Notification sent: $($response1.success)" -ForegroundColor Green
Write-Host ""

# Test 2: Send image
Write-Host "Test 2: Sending image" -ForegroundColor Yellow
$message2 = @{
    action = "image"
    url = "https://httpbin.org/image/jpeg"  # 标准JPEG格式，约35KB，国际稳定访问
}

$response2 = Invoke-RestMethod -Uri "http://localhost:5000/admin/device/send-message" `
    -Method POST `
    -ContentType "application/json" `
    -Body (@{
        userId = $userId
        message = $message2
    } | ConvertTo-Json -Depth 10)

Write-Host "Image sent: $($response2.success)" -ForegroundColor Green
Write-Host ""

# Test 3: Send audio URL
Write-Host "Test 3: Sending audio" -ForegroundColor Yellow
$message3 = @{
    action = "audio"
    url = "http://172.20.10.8:5000/test-audio.mp3"  # 本地测试音频（需在wwwroot目录下放置test-audio.mp3）
}

$response3 = Invoke-RestMethod -Uri "http://localhost:5000/admin/device/send-message" `
    -Method POST `
    -ContentType "application/json" `
    -Body (@{
        userId = $userId
        message = $message3
    } | ConvertTo-Json -Depth 10)

Write-Host "Audio sent: $($response3.success)" -ForegroundColor Green
Write-Host ""

# Test 4: Send text display
Write-Host "Test 4: Sending text display" -ForegroundColor Yellow
$message4 = @{
    action = "display"
    content = "Hello from Verdure MCP!"
    role = "assistant"
}

$response4 = Invoke-RestMethod -Uri "http://localhost:5000/admin/device/send-message" `
    -Method POST `
    -ContentType "application/json" `
    -Body (@{
        userId = $userId
        message = $message4
    } | ConvertTo-Json -Depth 10)

Write-Host "Display sent: $($response4.success)" -ForegroundColor Green
Write-Host ""

# Test 5: Send command
Write-Host "Test 5: Sending command" -ForegroundColor Yellow
$message5 = @{
    action = "command"
    command = "listen"
}

$response5 = Invoke-RestMethod -Uri "http://localhost:5000/admin/device/send-message" `
    -Method POST `
    -ContentType "application/json" `
    -Body (@{
        userId = $userId
        message = $message5
    } | ConvertTo-Json -Depth 10)

Write-Host "Command sent: $($response5.success)" -ForegroundColor Green
Write-Host ""

# Test 6: Send emotion
Write-Host "Test 6: Sending emotion" -ForegroundColor Yellow
$message6 = @{
    action = "emotion"
    emotion = "happy"
}

$response6 = Invoke-RestMethod -Uri "http://localhost:5000/admin/device/send-message" `
    -Method POST `
    -ContentType "application/json" `
    -Body (@{
        userId = $userId
        message = $message6
    } | ConvertTo-Json -Depth 10)

Write-Host "Emotion sent: $($response6.success)" -ForegroundColor Green
Write-Host ""

# Test 7: Get all active connections
Write-Host "Test 7: Getting all active device connections" -ForegroundColor Yellow
$connections = Invoke-RestMethod -Uri "http://localhost:5000/admin/device/connections" `
    -Method GET

Write-Host "Active connections:" -ForegroundColor Green
$connections | ForEach-Object {
    Write-Host "  ConnectionId: $($_.connectionId)"
    Write-Host "  UserId: $($_.userId)"
    Write-Host "  Connected: $($_.connectedAt)"
    Write-Host "  Last Heartbeat: $($_.lastHeartbeatAt)"
    if ($_.device) {
        Write-Host "  Device:"
        Write-Host "    MAC: $($_.device.macAddress)"
        Write-Host "    Status: $($_.device.status)"
        Write-Host "    Metadata: $($_.device.metadata)"
    } else {
        Write-Host "  Device: Not registered yet" -ForegroundColor Yellow
    }
    Write-Host ""
}

# Test 8: Send to specific connection
if ($connections.Count -gt 0) {
    $connectionId = $connections[0].connectionId
    Write-Host "Test 8: Sending direct message to ConnectionId: $connectionId" -ForegroundColor Yellow
    
    $directMessage = @{
        action = "notification"
        title = "Direct Message"
        content = "This is a targeted message"
        emotion = "mail"
        sound = "success"
    }
    
    $response8 = Invoke-RestMethod -Uri "http://localhost:5000/admin/device/send-to-connection" `
        -Method POST `
        -ContentType "application/json" `
        -Body (@{
            connectionId = $connectionId
            message = $directMessage
        } | ConvertTo-Json -Depth 10)
    
    Write-Host "Direct message sent: $($response8.success)" -ForegroundColor Green
}

Write-Host ""
Write-Host "Tests completed! Check ESP32 logs for CustomMessage events." -ForegroundColor Cyan
