using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Data;

namespace WebApi.Controllers.Core;

/// <summary>
/// Health check endpoint for monitoring service status
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class HealthController(
    AppDbContext context, 
    ILogger<HealthController> logger) 
    : ControllerBase
{
    /// <summary>
    /// Get detailed health status of the API and its dependencies
    /// </summary>
    /// <returns>Health status information</returns>
    /// <response code="200">Service is healthy</response>
    /// <response code="503">Service is unhealthy</response>
    [HttpGet]
    [ProducesResponseType(typeof(HealthStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthStatus), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthStatus>> GetHealth()
    {
        var health = new HealthStatus
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = GetType().Assembly.GetName().Version?.ToString() ?? "Unknown"
        };

        // Check database connectivity
        try
        {
            if (context.Database.IsRelational())
            {
                var canConnect = await context.Database.CanConnectAsync();
                health.Database = new DatabaseHealth
                {
                    Status = canConnect ? "Connected" : "Disconnected",
                    Provider = context.Database.ProviderName ?? "Unknown"
                };
            }
            else
            {
                health.Database = new DatabaseHealth
                {
                    Status = "InMemory",
                    Provider = "InMemory"
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database health check failed");
            health.Database = new DatabaseHealth
            {
                Status = "Error",
                Provider = context.Database.ProviderName ?? "Unknown",
                Error = ex.Message
            };
            health.Status = "Unhealthy";
        }

        // Determine overall status
        if (health.Status == "Unhealthy" || 
            (health.Database != null && health.Database.Status == "Error"))
        {
            return StatusCode(503, health);
        }

        return Ok(health);
    }
}

/// <summary>
/// Health status response model
/// </summary>
public class HealthStatus
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Version { get; set; } = string.Empty;
    public DatabaseHealth? Database { get; set; }
}

/// <summary>
/// Database health information
/// </summary>
public class DatabaseHealth
{
    public string Status { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? Error { get; set; }
}
