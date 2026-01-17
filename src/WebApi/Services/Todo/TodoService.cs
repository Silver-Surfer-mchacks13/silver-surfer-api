using Microsoft.EntityFrameworkCore;
using WebApi.Data;
using WebApi.DTOs.Tags;
using WebApi.DTOs.Todos;
using WebApi.Models;

namespace WebApi.Services.Todo;

public class TodoService(AppDbContext context)
{
    public async Task<List<TodoItemDto>> GetAllTodosAsync(Guid userId, string? status = null, Priority? priority = null, Guid? tagId = null)
    {
        // Only include tags if filtering by tag or if we need tag data
        var needsTags = tagId.HasValue;
        
        var query = context.TodoItems
            .Where(t => t.UserId == userId);

        // Filter by status
        if (!string.IsNullOrEmpty(status))
        {
            if (status.Equals("completed", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(t => t.IsCompleted);
            }
            else if (status.Equals("pending", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(t => !t.IsCompleted);
            }
        }

        // Filter by priority
        if (priority.HasValue)
        {
            query = query.Where(t => t.Priority == priority.Value);
        }

        // Filter by tag
        if (tagId.HasValue)
        {
            query = query.Where(t => t.Tags.Any(tag => tag.Id == tagId.Value));
        }

        // Include tags only when needed
        if (needsTags)
        {
            query = query.Include(t => t.Tags);
        }

        var todos = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        // If we didn't include tags, load them separately for the DTOs
        if (!needsTags)
        {
            foreach (var todo in todos)
            {
                await context.Entry(todo).Collection(t => t.Tags).LoadAsync();
            }
        }

        return todos.Select(MapToDto).ToList();
    }

    private static DateTime? NormalizeToUtc(DateTime? dateTime)
    {
        if (!dateTime.HasValue)
            return null;
        
        var dt = dateTime.Value;
        if (dt.Kind == DateTimeKind.Unspecified)
        {
            // Treat Unspecified as UTC (assume incoming dates are already in UTC)
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }
        
        if (dt.Kind == DateTimeKind.Local)
        {
            // Convert Local to UTC
            return dt.ToUniversalTime();
        }
        
        // Already UTC
        return dt;
    }

    public async Task<TodoItemDto?> GetTodoByIdAsync(Guid todoId, Guid userId)
    {
        var todo = await context.TodoItems
            .Include(t => t.Tags)
            .FirstOrDefaultAsync(t => t.Id == todoId && t.UserId == userId);

        return todo == null ? null : MapToDto(todo);
    }

    public async Task<TodoItemDto> CreateTodoAsync(CreateTodoRequest request, Guid userId)
    {

        var todo = new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            DueDate = NormalizeToUtc(request.DueDate),
            IsCompleted = false,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Add tags if specified
        if (request.TagIds != null && request.TagIds.Any())
        {
            var tags = await context.Tags
                .Where(t => request.TagIds.Contains(t.Id) && t.UserId == userId)
                .ToListAsync();
            
            foreach (var tag in tags)
            {
                todo.Tags.Add(tag);
            }
        }

        context.TodoItems.Add(todo);
        await context.SaveChangesAsync();

        // Reload with tags
        await context.Entry(todo).Collection(t => t.Tags).LoadAsync();

        return MapToDto(todo);
    }

    public async Task<TodoItemDto?> UpdateTodoAsync(Guid todoId, UpdateTodoRequest request, Guid userId)
    {
        var todo = await context.TodoItems
            .Include(t => t.Tags)
            .FirstOrDefaultAsync(t => t.Id == todoId && t.UserId == userId);

        if (todo == null)
        {
            return null;
        }

        todo.Title = request.Title;
        todo.Description = request.Description;
        todo.Priority = request.Priority;
        todo.DueDate = NormalizeToUtc(request.DueDate);
        todo.UpdatedAt = DateTime.UtcNow;

        // Update tags
        if (request.TagIds != null)
        {
            // Remove existing tags
            todo.Tags.Clear();

            // Add new tags
            if (request.TagIds.Any())
            {
                var tags = await context.Tags
                    .Where(t => request.TagIds.Contains(t.Id) && t.UserId == userId)
                    .ToListAsync();
                
                foreach (var tag in tags)
                {
                    todo.Tags.Add(tag);
                }
            }
        }

        await context.SaveChangesAsync();

        return MapToDto(todo);
    }

    public async Task<TodoItemDto?> ToggleCompleteAsync(Guid todoId, Guid userId)
    {
        var todo = await context.TodoItems
            .Include(t => t.Tags)
            .FirstOrDefaultAsync(t => t.Id == todoId && t.UserId == userId);

        if (todo == null)
        {
            return null;
        }

        todo.IsCompleted = !todo.IsCompleted;
        todo.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return MapToDto(todo);
    }

    public async Task<bool> DeleteTodoAsync(Guid todoId, Guid userId)
    {
        var todo = await context.TodoItems
            .FirstOrDefaultAsync(t => t.Id == todoId && t.UserId == userId);

        if (todo == null)
        {
            return false;
        }

        context.TodoItems.Remove(todo);
        await context.SaveChangesAsync();

        return true;
    }

    private static TodoItemDto MapToDto(TodoItem todo)
    {
        return new TodoItemDto
        {
            Id = todo.Id,
            Title = todo.Title,
            Description = todo.Description,
            IsCompleted = todo.IsCompleted,
            Priority = todo.Priority,
            DueDate = todo.DueDate,
            Tags = todo.Tags.Select(t => new TagDto
            {
                Id = t.Id,
                Name = t.Name,
                Color = t.Color,
                TodoCount = 0 // Not calculated here for performance
            }).ToList(),
            CreatedAt = todo.CreatedAt,
            UpdatedAt = todo.UpdatedAt
        };
    }
}
