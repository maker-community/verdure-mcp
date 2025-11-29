using Microsoft.JSInterop;

namespace Verdure.Mcp.Web.Services;

public interface IClipboardService
{
    ValueTask CopyToClipboardAsync(string text);
}

public class ClipboardService : IClipboardService
{
    private readonly IJSRuntime _jsRuntime;

    public ClipboardService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async ValueTask CopyToClipboardAsync(string text)
    {
        await _jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
    }
}
