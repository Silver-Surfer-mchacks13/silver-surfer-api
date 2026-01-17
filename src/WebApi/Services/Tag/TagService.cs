using Microsoft.EntityFrameworkCore;
using WebApi.Data;
using WebApi.DTOs.Tags;
using WebApi.Exceptions;

namespace WebApi.Services.Tag;

public class TagService(AppDbContext context)
{
    public async Task<List<TagDto>> GetAllTagsAsync(Guid userId)
    {
        var tags = await context.Tags
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Name)
            .Select(t => new
            {
                Tag = t,
                TodoCount = t.TodoItems.Count
            })
            .ToListAsync();

        return tags.Select(x => new TagDto
        {
            Id = x.Tag.Id,
            Name = x.Tag.Name,
            Color = x.Tag.Color,
            TodoCount = x.TodoCount
        }).ToList();
    }

    public async Task<TagDto?> GetTagByIdAsync(Guid tagId, Guid userId)
    {
        var result = await context.Tags
            .Where(t => t.Id == tagId && t.UserId == userId)
            .Select(t => new
            {
                Tag = t,
                TodoCount = t.TodoItems.Count
            })
            .FirstOrDefaultAsync();

        if (result == null)
        {
            return null;
        }

        return new TagDto
        {
            Id = result.Tag.Id,
            Name = result.Tag.Name,
            Color = result.Tag.Color,
            TodoCount = result.TodoCount
        };
    }

    public async Task<TagDto> CreateTagAsync(CreateTagRequest request, Guid userId)
    {
        // Check if tag name already exists for this user
        if (await context.Tags.AnyAsync(t => t.UserId == userId && t.Name == request.Name))
        {
            throw new ConflictException("A tag with this name already exists");
        }

        var tag = new Models.Tag
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Color = request.Color,
            UserId = userId
        };

        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        return new TagDto
        {
            Id = tag.Id,
            Name = tag.Name,
            Color = tag.Color,
            TodoCount = 0 // New tag has no todos
        };
    }

    public async Task<TagDto?> UpdateTagAsync(Guid tagId, UpdateTagRequest request, Guid userId)
    {
        var tag = await context.Tags
            .Where(t => t.Id == tagId && t.UserId == userId)
            .FirstOrDefaultAsync();

        if (tag == null)
        {
            return null;
        }

        // Check if new name conflicts with existing tag
        if (tag.Name != request.Name && 
            await context.Tags.AnyAsync(t => t.UserId == userId && t.Name == request.Name && t.Id != tagId))
        {
            throw new ConflictException("A tag with this name already exists");
        }

        tag.Name = request.Name;
        tag.Color = request.Color;

        await context.SaveChangesAsync();

        // Get todo count after update
        var todoCount = await context.TodoItems
            .CountAsync(t => t.Tags.Any(tag => tag.Id == tagId));

        return new TagDto
        {
            Id = tag.Id,
            Name = tag.Name,
            Color = tag.Color,
            TodoCount = todoCount
        };
    }

    public async Task<bool> DeleteTagAsync(Guid tagId, Guid userId)
    {
        // Must remove many-to-many join rows first; the join table uses DeleteBehavior.NoAction
        // so PostgreSQL will reject deleting a Tag that is still referenced by TodoItemTag.
        var tag = await context.Tags
            .Include(t => t.TodoItems)
            .FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId);

        if (tag == null)
        {
            return false;
        }

        // Remove association from all todos first (deletes join rows)
        if (tag.TodoItems.Count > 0)
        {
            tag.TodoItems.Clear();
            await context.SaveChangesAsync();
        }

        context.Tags.Remove(tag);
        await context.SaveChangesAsync();

        return true;
    }
}

