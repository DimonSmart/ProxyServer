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
}
