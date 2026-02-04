# AsyncLocal<T> 快速参考卡片

## ✅ 优势

- **线程安全**: 每个异步执行流有独立的副本
- **自动传播**: 通过 `async/await` 自动流动，无需手动传递
- **零侵入**: 不需要修改所有方法签名
- **自动清理**: 随 ExecutionContext 生命周期自动管理

## ⚠️ 常见陷阱

| 陷阱 | 说明 | 解决方案 |
|------|------|----------|
| **Task.Run** | 创建新的 ExecutionContext，不继承父上下文 | 使用 `await` 而非 `Task.Run` |
| **同步阻塞** | `.Result` 或 `.Wait()` 可能导致死锁 | 使用 `await` |
| **Parallel.ForEach** | 不保留 ExecutionContext | 使用异步 LINQ 或显式传参 |
| **Hangfire 任务** | 独立的执行上下文 | 通过参数传递，在任务内重新设置 |

## 🎯 最佳实践

### ✅ DO

```csharp
// 1. 在执行链开始设置
UserContext.Current = new UserContext { UserId = "user-123" };

// 2. 使用 async/await 链
await Method1();
await Method2();

// 3. 只存储轻量级数据
public class UserContext
{
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
}

// 4. 添加验证日志
if (UserContext.Current == null)
{
    _logger.LogWarning("UserContext not set");
}
```

### ❌ DON'T

```csharp
// ❌ 避免 Task.Run
await Task.Run(async () => { ... });

// ❌ 避免同步阻塞
var result = SomeAsyncMethod().Result;

// ❌ 避免存储大对象
UserContext.Current = new UserContext 
{ 
    LargeData = new byte[1024 * 1024 * 100] 
};
```

## 🔍 故障排查

### UserContext.Current 为 null？

**检查清单**:
- [ ] 是否在调用链开始处设置了 UserContext？
- [ ] 中间是否使用了 `Task.Run`？
- [ ] 是否使用了 `.Result` 或 `.Wait()`？
- [ ] 是否所有方法都使用了 `async/await`？

**调试代码**:
```csharp
_logger.LogDebug("UserContext: {Status}", 
    UserContext.Current != null ? "SET" : "NULL");
```

## 📊 性能数据

- **内存开销**: ~150 bytes per request (UserContext)
- **CPU 开销**: 可忽略不计
- **GC 压力**: 极低，自动回收

## 🎓 核心原理

```
设置 UserContext
    ↓
ExecutionContext 捕获
    ↓
await (自动传播)
    ↓
可能切换线程，但 ExecutionContext 跟随
    ↓
读取 UserContext (相同的值) ✅
```

## 🔗 相关文档

- [ASYNCLOCAL_DEEP_DIVE.md](ASYNCLOCAL_DEEP_DIVE.md) - 完整技术文档
- [MCP_USER_CONTEXT_FORWARDING.md](MCP_USER_CONTEXT_FORWARDING.md) - 实现文档

---

**记住**: AsyncLocal 依赖于正确的 async/await 模式！
