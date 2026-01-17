using Microsoft.AspNetCore.Mvc;
using WebApi.Configuration;

namespace WebApi.Controllers.Core;

/// <summary>
/// Debug endpoint to show configuration values (for troubleshooting)
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class ConfigController(
    IConfiguration configuration,
    IHostEnvironment environment) 
    : ControllerBase
{
    /// <summary>
    /// Shows the CORS configuration that's actually being used
    /// </summary>
    [HttpGet("cors")]
    public IActionResult GetCorsConfig()
    {
        // Use the exact same logic as Program.cs
        var corsEnabled = ServiceConfiguration.IsCorsEnabled(configuration);
        var resolvedOrigins = ServiceConfiguration.GetCorsAllowedOrigins(configuration);

        // Check configuration key for debugging
        var corsOriginsString = configuration["Cors__AllowedOrigins"]
                             ?? Environment.GetEnvironmentVariable("Cors__AllowedOrigins");

        var corsOriginsArray = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        var corsEnabledConfig = configuration.GetValue<bool?>("Cors:Enabled");
        var corsEnabledEnvVar = Environment.GetEnvironmentVariable("Cors__Enabled");

        // Get all environment variables that start with "Cors" for debugging
        var allCorsEnvVars = Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .Where(e => e.Key.ToString()?.StartsWith("Cors", StringComparison.OrdinalIgnoreCase) == true)
            .ToDictionary(e => e.Key.ToString()!, e => e.Value?.ToString() ?? "(null)");

        // Build response showing what we found
        var response = new
        {
            corsEnabled = corsEnabled,
            corsEnabledConfig = corsEnabledConfig,
            corsEnabledEnvVar = corsEnabledEnvVar ?? "(not set)",
            corsOriginsString = corsOriginsString ?? "(null)",
            corsOriginsArray = corsOriginsArray ?? Array.Empty<string>(),
            corsOriginsArrayLength = corsOriginsArray?.Length ?? 0,
            environmentVariable = Environment.GetEnvironmentVariable("Cors__AllowedOrigins") ?? "(not set)",
            allCorsEnvironmentVariables = allCorsEnvVars,
            allConfigKeys = new
            {
                corsEnabled = configuration["Cors:Enabled"] ?? "(null)",
                corsAllowedOrigins = configuration["Cors__AllowedOrigins"] ?? "(null)",
            },
            resolvedOrigins = resolvedOrigins,
            resolvedOriginsCount = resolvedOrigins.Length,
            willAllowAllOrigins = resolvedOrigins.Length == 0
        };

        return Ok(response);
    }

#if DEBUG
    /// <summary>
    /// Returns all configuration values as JSON (development only)
    /// </summary>
    /// <returns>Complete configuration as JSON</returns>
    /// <response code="200">Returns all configuration values</response>
    /// <response code="404">Not available in production</response>
    [HttpGet("all")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetAllConfig()
    {
        if (environment.IsProduction())
        {
            return NotFound("Configuration endpoint is only available in development");
        }

        var configDict = GetConfigurationAsDictionary(configuration);
        return Ok(configDict);
    }
#endif

    private static Dictionary<string, object?> GetConfigurationAsDictionary(IConfiguration configuration)
    {
        var result = new Dictionary<string, object?>();

        foreach (var child in configuration.GetChildren())
        {
            result[child.Key] = GetConfigurationValue(child);
        }

        return result;
    }

    private static object? GetConfigurationValue(IConfigurationSection section)
    {
        var children = section.GetChildren().ToList();
        
        if (children.Count == 0)
        {
            // Leaf node - return the value
            return section.Value;
        }

        // Has children - return as dictionary
        var dict = new Dictionary<string, object?>();
        foreach (var child in children)
        {
            dict[child.Key] = GetConfigurationValue(child);
        }

        // If there's also a value at this level, include it
        if (!string.IsNullOrEmpty(section.Value))
        {
            dict["_value"] = section.Value;
        }

        return dict;
    }
}

