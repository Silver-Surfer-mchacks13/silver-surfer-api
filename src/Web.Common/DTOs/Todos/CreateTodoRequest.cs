using System.ComponentModel.DataAnnotations;
using WebApi.Models;

namespace WebApi.DTOs.Todos;

public class CreateTodoRequest
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(200, ErrorMessage = "Title must not exceed 200 characters")]
    public required string Title { get; set; }
    
    [StringLength(1000, ErrorMessage = "Description must not exceed 1000 characters")]
    public string? Description { get; set; }
    
    public Priority Priority { get; set; }
    
    public DateTime? DueDate { get; set; }
    
    public List<Guid>? TagIds { get; set; }
}


