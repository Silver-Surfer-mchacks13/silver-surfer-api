using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Web.Common.DTOs;
using Web.Common.DTOs.Analyze;
using WebApi.Services.Gemini;

namespace WebApi.Controllers;

/// <summary>
/// Manages web page analysis using Gemini AI
/// </summary>
/// <remarks>
/// TODO: To add authentication, inherit from BaseController and remove [AllowAnonymous]
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[AllowAnonymous]
[EnableRateLimiting("Global")]
public class AnalyzeController : ControllerBase
{
    private readonly GeminiService _geminiService;
    private readonly ILogger<AnalyzeController> _logger;

    public AnalyzeController(GeminiService geminiService, ILogger<AnalyzeController> logger)
    {
        _geminiService = geminiService;
        _logger = logger;
    }

    /// <summary>
    /// Analyze a webpage using Gemini AI
    /// </summary>
    /// <param name="request">The analysis request containing prompt, HTML content, and screenshot</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Analysis result from Gemini</returns>
    /// <response code="200">Returns the analysis result</response>
    /// <response code="400">Invalid request data</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [ProducesResponseType(typeof(AnalyzeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AnalyzeResponse>> Analyze(
        [FromBody] AnalyzeRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Invalid request data",
                Errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>())
            });
        }

        _logger.LogInformation("Received analyze request. Prompt length: {PromptLength}", request.Prompt.Length);

        var response = await _geminiService.AnalyzeWebPageAsync(request, cancellationToken);

        if (!response.Success)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Message = response.Error ?? "Analysis failed",
                ErrorCode = "ANALYSIS_FAILED"
            });
        }

        return Ok(response);
    }
}
