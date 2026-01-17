using System.Diagnostics.CodeAnalysis;

namespace WebApi.Models;

/// <summary>
/// Learned patterns per domain for future optimization (optional, reserved for future ML features)
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class SitePattern
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Domain name (e.g., "example.com")
    /// </summary>
    public required string Domain { get; set; }
    
    /// <summary>
    /// JSON string containing learned patterns for this domain
    /// </summary>
    public required string PatternJson { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
}
