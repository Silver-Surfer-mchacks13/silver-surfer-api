using Microsoft.AspNetCore.Mvc;
using Web.Common.DTOs;
using WebApi.Controllers.Utils;
using WebApi.DTOs;
using WebApi.DTOs.Todos;
using WebApi.Exceptions;
using WebApi.Models;
using WebApi.Services;
using WebApi.Services.Todo;

namespace WebApi.Controllers;

/// <summary>
/// Manages todo items for authenticated users
/// </summary>
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class TodosController(
    TodoService todoService) 
    : BaseController
{
    /// <summary>
    /// Get all todos for the authenticated user with optional filtering
    /// </summary>
    /// <param name="status">Filter by completion status (all/completed/pending)</param>
    /// <param name="priority">Filter by priority level (low/medium/high)</param>
    /// <param name="tag">Filter by tag ID</param>
    /// <param name="isCompleted">Alias for status filter: true = completed, false = pending</param>
    /// <param name="tagId">Alias for tag filter (same as tag)</param>
    /// <returns>List of todos matching the filter criteria</returns>
    /// <response code="200">Returns the list of todos</response>
    /// <response code="401">User not authenticated</response>
    /// <remarks>
    /// Sample requests:
    ///
    ///     GET /api/v1/todos
    ///     GET /api/v1/todos?status=completed
    ///     GET /api/v1/todos?priority=high
    ///     GET /api/v1/todos?status=pending&amp;priority=medium
    ///     GET /api/v1/todos?tag=550e8400-e29b-41d4-a716-446655440000
    ///     GET /api/v1/todos?isCompleted=false
    ///     GET /api/v1/todos?tagId=550e8400-e29b-41d4-a716-446655440000
    ///
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(List<TodoItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<TodoItemDto>>> GetAllTodos(
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] Guid? tag = null,
        // Back-compat / test-friendly aliases:
        [FromQuery] bool? isCompleted = null,
        [FromQuery] Guid? tagId = null)
    {
        var userId = GetUserId();

        // If the caller uses the bool-style filter (?isCompleted=true/false), map it to status.
        if (string.IsNullOrEmpty(status) && isCompleted.HasValue)
        {
            status = isCompleted.Value ? "completed" : "pending";
        }

        // If the caller uses ?tagId=..., map it to the existing tag filter.
        tag ??= tagId;
        
        Priority? priorityEnum = null;
        if (!string.IsNullOrEmpty(priority) && Enum.TryParse<Priority>(priority, true, out var parsedPriority))
        {
            priorityEnum = parsedPriority;
        }

        var todos = await todoService.GetAllTodosAsync(userId, status, priorityEnum, tag);
        return Ok(todos);
    }

    /// <summary>
    /// Get a specific todo by ID
    /// </summary>
    /// <param name="id">The unique identifier of the todo item</param>
    /// <returns>The requested todo item</returns>
    /// <response code="200">Returns the todo item</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="404">Todo not found or doesn't belong to user</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TodoItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TodoItemDto>> GetTodoById(Guid id)
    {
        var userId = GetUserId();
        var todo = await todoService.GetTodoByIdAsync(id, userId);

        if (todo == null)
        {
            return this.NotFoundError("Todo not found");
        }

        return Ok(todo);
    }

    /// <summary>
    /// Create a new todo item
    /// </summary>
    /// <param name="request">Todo creation details</param>
    /// <returns>The newly created todo item</returns>
    /// <response code="201">Todo created successfully</response>
    /// <response code="400">Invalid input data</response>
    /// <response code="401">User not authenticated</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/v1/todos
    ///     {
    ///        "title": "Complete project documentation",
    ///        "description": "Write comprehensive API documentation",
    ///        "priority": 1,
    ///        "dueDate": "2026-01-15T12:00:00Z",
    ///        "tagIds": ["550e8400-e29b-41d4-a716-446655440000"]
    ///     }
    ///
    /// Priority values: 0 = Low, 1 = Medium, 2 = High
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(TodoItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TodoItemDto>> CreateTodo([FromBody] CreateTodoRequest request)
    {
        try
        {
            var userId = GetUserId();
            var todo = await todoService.CreateTodoAsync(request, userId);
            return CreatedAtAction(nameof(GetTodoById), new { id = todo.Id }, todo);
        }
        catch (ValidationException ex)
        {
            return this.BadRequestError(ex.Message);
        }
        catch (NotFoundException ex)
        {
            return this.NotFoundError(ex.Message);
        }
    }

    /// <summary>
    /// Update an existing todo item
    /// </summary>
    /// <param name="id">The unique identifier of the todo to update</param>
    /// <param name="request">Updated todo details</param>
    /// <returns>The updated todo item</returns>
    /// <response code="200">Todo updated successfully</response>
    /// <response code="400">Invalid input data</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="404">Todo not found or doesn't belong to user</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT /api/v1/todos/550e8400-e29b-41d4-a716-446655440000
    ///     {
    ///        "title": "Updated task title",
    ///        "description": "Updated description",
    ///        "priority": 2,
    ///        "dueDate": "2026-01-20T12:00:00Z",
    ///        "tagIds": []
    ///     }
    ///
    /// </remarks>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TodoItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TodoItemDto>> UpdateTodo(Guid id, [FromBody] UpdateTodoRequest request)
    {
        try
        {
            var userId = GetUserId();
            var todo = await todoService.UpdateTodoAsync(id, request, userId);

            if (todo == null)
            {
                return this.NotFoundError("Todo not found");
            }

            return Ok(todo);
        }
        catch (ValidationException ex)
        {
            return this.BadRequestError(ex.Message);
        }
        catch (NotFoundException ex)
        {
            return this.NotFoundError(ex.Message);
        }
    }

    /// <summary>
    /// Toggle the completion status of a todo item
    /// </summary>
    /// <param name="id">The unique identifier of the todo</param>
    /// <returns>The updated todo item with toggled completion status</returns>
    /// <response code="200">Completion status toggled successfully</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="404">Todo not found or doesn't belong to user</response>
    /// <remarks>
    /// This endpoint toggles the IsCompleted property. If the todo is completed, it will be marked as pending, and vice versa.
    /// 
    /// Sample request:
    ///
    ///     PATCH /api/v1/todos/550e8400-e29b-41d4-a716-446655440000/complete
    ///
    /// </remarks>
    [HttpPatch("{id}/complete")]
    [ProducesResponseType(typeof(TodoItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TodoItemDto>> ToggleComplete(Guid id)
    {
        var userId = GetUserId();
        var todo = await todoService.ToggleCompleteAsync(id, userId);

        if (todo == null)
        {
            return this.NotFoundError("Todo not found");
        }

        return Ok(todo);
    }

    /// <summary>
    /// Delete a todo item
    /// </summary>
    /// <param name="id">The unique identifier of the todo to delete</param>
    /// <returns>No content on success</returns>
    /// <response code="204">Todo deleted successfully</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="404">Todo not found or doesn't belong to user</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTodo(Guid id)
    {
        var userId = GetUserId();
        var result = await todoService.DeleteTodoAsync(id, userId);

        if (!result)
        {
            return this.NotFoundError("Todo not found");
        }

        return NoContent();
    }
}

