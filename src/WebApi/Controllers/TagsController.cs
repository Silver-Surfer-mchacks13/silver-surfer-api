using Microsoft.AspNetCore.Mvc;
using Web.Common.DTOs;
using WebApi.Controllers.Utils;
using WebApi.DTOs;
using WebApi.DTOs.Tags;
using WebApi.Exceptions;
using WebApi.Services;
using WebApi.Services.Tag;

namespace WebApi.Controllers;

/// <summary>
/// Manages tags for categorizing todo items
/// </summary>
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class TagsController(
    TagService tagService) 
    : BaseController
{
    /// <summary>
    /// Get all tags for the authenticated user
    /// </summary>
    /// <returns>List of all tags with todo counts</returns>
    /// <response code="200">Returns the list of tags</response>
    /// <response code="401">User not authenticated</response>
    /// <remarks>
    /// Each tag includes the count of todo items associated with it.
    /// Tags are returned sorted alphabetically by name.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(List<TagDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<TagDto>>> GetAllTags()
    {
        var userId = GetUserId();
        var tags = await tagService.GetAllTagsAsync(userId);
        return Ok(tags);
    }

    /// <summary>
    /// Get a specific tag by ID
    /// </summary>
    /// <param name="id">The unique identifier of the tag</param>
    /// <returns>The requested tag with todo count</returns>
    /// <response code="200">Returns the tag</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="404">Tag not found or doesn't belong to user</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TagDto>> GetTagById(Guid id)
    {
        var userId = GetUserId();
        var tag = await tagService.GetTagByIdAsync(id, userId);

        if (tag == null)
        {
            return this.NotFoundError("Tag not found");
        }

        return Ok(tag);
    }

    /// <summary>
    /// Create a new tag
    /// </summary>
    /// <param name="request">Tag creation details</param>
    /// <returns>The newly created tag</returns>
    /// <response code="201">Tag created successfully</response>
    /// <response code="400">Invalid input or tag name already exists</response>
    /// <response code="401">User not authenticated</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/v1/tags
    ///     {
    ///        "name": "Work",
    ///        "color": "#FF5733"
    ///     }
    ///
    /// Tag names must be unique per user. 
    /// Color should be in hex format (#RRGGBB).
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(TagDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TagDto>> CreateTag([FromBody] CreateTagRequest request)
    {
        try
        {
            var userId = GetUserId();
            var tag = await tagService.CreateTagAsync(request, userId);
            return CreatedAtAction(nameof(GetTagById), new { id = tag.Id }, tag);
        }
        catch (ValidationException ex)
        {
            return this.BadRequestError(ex.Message);
        }
        catch (ConflictException ex)
        {
            return this.ConflictError(ex.Message);
        }
    }

    /// <summary>
    /// Update an existing tag
    /// </summary>
    /// <param name="id">The unique identifier of the tag to update</param>
    /// <param name="request">Updated tag details</param>
    /// <returns>The updated tag</returns>
    /// <response code="200">Tag updated successfully</response>
    /// <response code="400">Invalid input or tag name already exists</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="404">Tag not found or doesn't belong to user</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT /api/v1/tags/550e8400-e29b-41d4-a716-446655440000
    ///     {
    ///        "name": "Personal",
    ///        "color": "#3498DB"
    ///     }
    ///
    /// When updating a tag, all associated todos will retain their association with the tag.
    /// </remarks>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TagDto>> UpdateTag(Guid id, [FromBody] UpdateTagRequest request)
    {
        try
        {
            var userId = GetUserId();
            var tag = await tagService.UpdateTagAsync(id, request, userId);

            if (tag == null)
            {
                return this.NotFoundError("Tag not found");
            }

            return Ok(tag);
        }
        catch (ValidationException ex)
        {
            return this.BadRequestError(ex.Message);
        }
        catch (ConflictException ex)
        {
            return this.ConflictError(ex.Message);
        }
    }

    /// <summary>
    /// Delete a tag
    /// </summary>
    /// <param name="id">The unique identifier of the tag to delete</param>
    /// <returns>No content on success</returns>
    /// <response code="204">Tag deleted successfully</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="404">Tag not found or doesn't belong to user</response>
    /// <remarks>
    /// When a tag is deleted, it will be removed from all associated todos.
    /// The todos themselves will not be deleted.
    /// </remarks>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTag(Guid id)
    {
        var userId = GetUserId();
        var result = await tagService.DeleteTagAsync(id, userId);

        if (!result)
        {
            return this.NotFoundError("Tag not found");
        }

        return NoContent();
    }
}

