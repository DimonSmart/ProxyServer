using Microsoft.AspNetCore.Mvc;
using ProxyServer.FunctionalTests.Models;

namespace ProxyServer.FunctionalTests.TestServer;

/// <summary>
/// Controller that provides string reversal functionality for testing proxy server
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StringReverseController : ControllerBase
{
    private static int _callCount = 0;

    /// <summary>
    /// Reverses the provided text string
    /// </summary>
    /// <param name="request">The request containing text to reverse</param>
    /// <returns>Response with original and reversed text</returns>
    [HttpPost("reverse")]
    public IActionResult ReverseString([FromBody] ReverseRequest request)
    {
        Interlocked.Increment(ref _callCount);

        if (string.IsNullOrEmpty(request.Text))
        {
            return BadRequest("Text is required");
        }

        var reversed = new string(request.Text.Reverse().ToArray());

        return Ok(new ReverseResponse
        {
            OriginalText = request.Text,
            ReversedText = reversed,
            CallNumber = _callCount,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Gets the current call statistics
    /// </summary>
    /// <returns>Statistics showing total number of calls</returns>
    [HttpGet("stats")]
    public IActionResult GetStats()
    {
        return Ok(new { CallCount = _callCount });
    }

    /// <summary>
    /// Resets the call statistics
    /// </summary>
    /// <returns>Confirmation message with reset statistics</returns>
    [HttpPost("reset")]
    public IActionResult ResetStats()
    {
        Interlocked.Exchange(ref _callCount, 0);
        return Ok(new { Message = "Stats reset", CallCount = _callCount });
    }
    
    /// <summary>
    /// Simulates a streaming response like Ollama by sending data in chunks with delays
    /// </summary>
    /// <param name="request">The request containing text to reverse</param>
    /// <returns>Streaming response with text sent in chunks</returns>
    [HttpPost("stream")]
    public async Task StreamReverse([FromBody] ReverseRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);

        if (string.IsNullOrEmpty(request.Text))
        {
            Response.StatusCode = 400;
            var errorBytes = System.Text.Encoding.UTF8.GetBytes("Text is required");
            await Response.Body.WriteAsync(errorBytes, 0, errorBytes.Length, cancellationToken);
            return;
        }

        Response.ContentType = "text/plain";
        
        var reversed = new string(request.Text.Reverse().ToArray());
        var chunks = new List<string>();
        
        // Split reversed text into chunks to simulate streaming
        for (int i = 0; i < reversed.Length; i += 3)
        {
            var chunk = reversed.Substring(i, Math.Min(3, reversed.Length - i));
            chunks.Add(chunk);
        }

        // Send chunks with small delays to simulate real streaming
        foreach (var chunk in chunks)
        {
            var chunkBytes = System.Text.Encoding.UTF8.GetBytes(chunk);
            await Response.Body.WriteAsync(chunkBytes, 0, chunkBytes.Length, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
            
            // Small delay to simulate processing time
            await Task.Delay(50, cancellationToken);
        }
        
        // Add final metadata
        var metadataBytes = System.Text.Encoding.UTF8.GetBytes($"\n[Call #{_callCount}]");
        await Response.Body.WriteAsync(metadataBytes, 0, metadataBytes.Length, cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
