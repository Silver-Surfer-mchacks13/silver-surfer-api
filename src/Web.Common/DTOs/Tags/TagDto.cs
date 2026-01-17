namespace WebApi.DTOs.Tags;

public class TagDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Color { get; set; }
    public int TodoCount { get; set; }
}


