using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using WebApi.Configuration;
using WebApi.Data;
using WebApi.Models;

Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);
builder.Environment.EnvironmentName = "Development";

// Get connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("Error: Connection string 'DefaultConnection' not found in configuration.");
    Environment.Exit(1);
}

// Create DbContext directly with connection string to avoid service provider lifecycle issues
// Add connection string parameters to prevent premature connection closure
var connectionStringBuilder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
{
    // Ensure connection stays open long enough
    CommandTimeout = 60,
    // Disable connection pooling for seeding to avoid disposal issues
    Pooling = false
};

var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(connectionStringBuilder.ConnectionString);
    // Disable retry for seeding to avoid connection disposal issues

using var dbContext = new AppDbContext(optionsBuilder.Options);

// Check if we should just drop tables (for reset script)
if (args.Length > 0 && args[0] == "--drop-tables")
{
    try
    {
        Console.WriteLine("Dropping all tables in asp_template schema...");
        var dropTablesSql = @"
DO $$ 
DECLARE
    r RECORD;
BEGIN
    FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'asp_template') 
    LOOP
        EXECUTE 'DROP TABLE IF EXISTS asp_template.' || quote_ident(r.tablename) || ' CASCADE';
    END LOOP;
END;
$$;
DROP TABLE IF EXISTS ""__EFMigrationsHistory"" CASCADE;";
        
        await dbContext.Database.ExecuteSqlRawAsync(dropTablesSql);
        Console.WriteLine("Successfully dropped all tables.");
        await dbContext.DisposeAsync();
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        await dbContext.DisposeAsync();
        return 1;
    }
}

Console.WriteLine("=== Todo App Database Seeding ===\n");

#region Sample Users

var user1 = new User
{
    Id = Guid.NewGuid(),
    Email = "alice@example.com",
    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!", workFactor: 12),
    Provider = AuthProvider.Local,
    ProviderUserId = null,
    CreatedAt = DateTime.UtcNow.AddDays(-30),
    UpdatedAt = DateTime.UtcNow.AddDays(-30)
};

var user2 = new User
{
    Id = Guid.NewGuid(),
    Email = "bob@example.com",
    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!", workFactor: 12),
    Provider = AuthProvider.Local,
    ProviderUserId = null,
    CreatedAt = DateTime.UtcNow.AddDays(-20),
    UpdatedAt = DateTime.UtcNow.AddDays(-20)
};

var user3 = new User
{
    Id = Guid.NewGuid(),
    Email = "charlie@example.com",
    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!", workFactor: 12),
    Provider = AuthProvider.Local,
    ProviderUserId = null,
    CreatedAt = DateTime.UtcNow.AddDays(-10),
    UpdatedAt = DateTime.UtcNow.AddDays(-10)
};

#endregion

#region Sample Tags

var workTag = new Tag
{
    Id = Guid.NewGuid(),
    Name = "Work",
    Color = "#FF5733",
    UserId = user1.Id
};

var personalTag = new Tag
{
    Id = Guid.NewGuid(),
    Name = "Personal",
    Color = "#3498DB",
    UserId = user1.Id
};

var urgentTag = new Tag
{
    Id = Guid.NewGuid(),
    Name = "Urgent",
    Color = "#E74C3C",
    UserId = user1.Id
};

var shoppingTag = new Tag
{
    Id = Guid.NewGuid(),
    Name = "Shopping",
    Color = "#9B59B6",
    UserId = user1.Id
};

var fitnessTag = new Tag
{
    Id = Guid.NewGuid(),
    Name = "Fitness",
    Color = "#2ECC71",
    UserId = user2.Id
};

var studyTag = new Tag
{
    Id = Guid.NewGuid(),
    Name = "Study",
    Color = "#F39C12",
    UserId = user2.Id
};

var projectTag = new Tag
{
    Id = Guid.NewGuid(),
    Name = "Project",
    Color = "#1ABC9C",
    UserId = user3.Id
};

var meetingTag = new Tag
{
    Id = Guid.NewGuid(),
    Name = "Meeting",
    Color = "#34495E",
    UserId = user3.Id
};

#endregion

#region Sample Todos for Alice

var todo1 = new TodoItem
{
    Id = Guid.NewGuid(),
    Title = "Complete project documentation",
    Description = "Write comprehensive API documentation for the new features",
    IsCompleted = false,
    Priority = Priority.High,
    DueDate = DateTime.UtcNow.AddDays(3),
    UserId = user1.Id,
    CreatedAt = DateTime.UtcNow.AddDays(-5),
    UpdatedAt = DateTime.UtcNow.AddDays(-5)
};

var todo2 = new TodoItem
{
    Id = Guid.NewGuid(),
    Title = "Review pull requests",
    Description = "Review and merge pending PRs from team members",
    IsCompleted = true,
    Priority = Priority.Medium,
    DueDate = DateTime.UtcNow.AddDays(-1),
    UserId = user1.Id,
    CreatedAt = DateTime.UtcNow.AddDays(-7),
    UpdatedAt = DateTime.UtcNow.AddDays(-1)
};

var todo3 = new TodoItem
{
    Id = Guid.NewGuid(),
    Title = "Buy groceries",
    Description = "Milk, eggs, bread, vegetables, and fruits",
    IsCompleted = false,
    Priority = Priority.Low,
    DueDate = DateTime.UtcNow.AddDays(1),
    UserId = user1.Id,
    CreatedAt = DateTime.UtcNow.AddHours(-12),
    UpdatedAt = DateTime.UtcNow.AddHours(-12)
};

var todo4 = new TodoItem
{
    Id = Guid.NewGuid(),
    Title = "Prepare presentation slides",
    Description = "Create slides for Monday's client meeting",
    IsCompleted = false,
    Priority = Priority.High,
    DueDate = DateTime.UtcNow.AddDays(2),
    UserId = user1.Id,
    CreatedAt = DateTime.UtcNow.AddDays(-3),
    UpdatedAt = DateTime.UtcNow.AddDays(-3)
};

var todo5 = new TodoItem
{
    Id = Guid.NewGuid(),
    Title = "Call dentist for appointment",
    Description = "Schedule a checkup appointment",
    IsCompleted = true,
    Priority = Priority.Medium,
    DueDate = null,
    UserId = user1.Id,
    CreatedAt = DateTime.UtcNow.AddDays(-10),
    UpdatedAt = DateTime.UtcNow.AddDays(-8)
};

var todo6 = new TodoItem
{
    Id = Guid.NewGuid(),
    Title = "Fix critical bug in production",
    Description = "Users reporting login issues - investigate and fix ASAP",
    IsCompleted = false,
    Priority = Priority.High,
    DueDate = DateTime.UtcNow.AddHours(6),
    UserId = user1.Id,
    CreatedAt = DateTime.UtcNow.AddHours(-2),
    UpdatedAt = DateTime.UtcNow.AddHours(-2)
};

#endregion

#region Sample Todos for Bob

var todo7 = new TodoItem
{
    Id = Guid.NewGuid(),
    Title = "Morning workout routine",
    Description = "30 minutes cardio + strength training",
    IsCompleted = true,
    Priority = Priority.Medium,
    DueDate = DateTime.UtcNow,
    UserId = user2.Id,
    CreatedAt = DateTime.UtcNow.AddDays(-15),
    UpdatedAt = DateTime.UtcNow.AddHours(-6)
};

var todo8 = new TodoItem
{
    Id = Guid.NewGuid(),
    Title = "Study for certification exam",
    Description = "Complete chapters 5-7 and practice questions",
    IsCompleted = false,
    Priority = Priority.High,
    DueDate = DateTime.UtcNow.AddDays(7),
    UserId = user2.Id,
    CreatedAt = DateTime.UtcNow.AddDays(-5),
    UpdatedAt = DateTime.UtcNow.AddDays(-5)
};

var todo9 = new TodoItem
{
    Id = Guid.NewGuid(),
    Title = "Meal prep for the week",
    Description = "Prepare healthy meals for Monday through Friday",
    IsCompleted = false,
    Priority = Priority.Low,
    DueDate = DateTime.UtcNow.AddDays(1),
    UserId = user2.Id,
    CreatedAt = DateTime.UtcNow.AddHours(-18),
    UpdatedAt = DateTime.UtcNow.AddHours(-18)
};

var todo10 = new TodoItem
{
    Id = Guid.NewGuid(),
    Title = "Read chapter 3 of textbook",
    Description = "Data structures and algorithms",
    IsCompleted = true,
    Priority = Priority.Medium,
    DueDate = DateTime.UtcNow.AddDays(-2),
    UserId = user2.Id,
    CreatedAt = DateTime.UtcNow.AddDays(-8),
    UpdatedAt = DateTime.UtcNow.AddDays(-3)
};

#endregion

#region Sample Todos for Charlie

var todo11 = new TodoItem
{
    Id = Guid.NewGuid(),
    Title = "Team standup meeting",
    Description = "Daily sync with development team at 10 AM",
    IsCompleted = true,
    Priority = Priority.Medium,
    DueDate = DateTime.UtcNow.AddHours(-2),
    UserId = user3.Id,
    CreatedAt = DateTime.UtcNow.AddDays(-1),
    UpdatedAt = DateTime.UtcNow.AddHours(-2)
};

var todo12 = new TodoItem
{
    Id = Guid.NewGuid(),
    Title = "Refactor authentication module",
    Description = "Improve code quality and add unit tests",
    IsCompleted = false,
    Priority = Priority.High,
    DueDate = DateTime.UtcNow.AddDays(5),
    UserId = user3.Id,
    CreatedAt = DateTime.UtcNow.AddDays(-4),
    UpdatedAt = DateTime.UtcNow.AddDays(-4)
};

var todo13 = new TodoItem
{
    Id = Guid.NewGuid(),
    Title = "Update project dependencies",
    Description = "Check for security vulnerabilities and update packages",
    IsCompleted = false,
    Priority = Priority.Medium,
    DueDate = DateTime.UtcNow.AddDays(10),
    UserId = user3.Id,
    CreatedAt = DateTime.UtcNow.AddDays(-6),
    UpdatedAt = DateTime.UtcNow.AddDays(-6)
};

var todo14 = new TodoItem
{
    Id = Guid.NewGuid(),
    Title = "Client presentation prep",
    Description = "Prepare demo environment and presentation materials",
    IsCompleted = false,
    Priority = Priority.High,
    DueDate = DateTime.UtcNow.AddDays(3),
    UserId = user3.Id,
    CreatedAt = DateTime.UtcNow.AddDays(-2),
    UpdatedAt = DateTime.UtcNow.AddDays(-2)
};

var todo15 = new TodoItem
{
    Id = Guid.NewGuid(),
    Title = "Code review session",
    Description = "Review new feature implementation with senior developer",
    IsCompleted = true,
    Priority = Priority.Low,
    DueDate = DateTime.UtcNow.AddDays(-1),
    UserId = user3.Id,
    CreatedAt = DateTime.UtcNow.AddDays(-5),
    UpdatedAt = DateTime.UtcNow.AddDays(-1)
};

#endregion

#region Associate Tags with Todos

todo1.Tags.Add(workTag);
todo1.Tags.Add(urgentTag);

todo2.Tags.Add(workTag);

todo3.Tags.Add(personalTag);
todo3.Tags.Add(shoppingTag);

todo4.Tags.Add(workTag);
todo4.Tags.Add(urgentTag);

todo5.Tags.Add(personalTag);

todo6.Tags.Add(workTag);
todo6.Tags.Add(urgentTag);

todo7.Tags.Add(fitnessTag);

todo8.Tags.Add(studyTag);

todo9.Tags.Add(fitnessTag);

todo10.Tags.Add(studyTag);

todo11.Tags.Add(meetingTag);
todo11.Tags.Add(projectTag);

todo12.Tags.Add(projectTag);

todo13.Tags.Add(projectTag);

todo14.Tags.Add(meetingTag);
todo14.Tags.Add(projectTag);

todo15.Tags.Add(projectTag);

#endregion

// Use simple transaction without retry strategy
using var transaction = await dbContext.Database.BeginTransactionAsync();
try
{
    Console.WriteLine("[1/5] Adding and saving users...");
    dbContext.Users.AddRange(user1, user2, user3);
    await dbContext.SaveChangesAsync();

    Console.WriteLine("[2/5] Adding and saving tags...");
    dbContext.Tags.AddRange(
        workTag, personalTag, urgentTag, shoppingTag,
        fitnessTag, studyTag,
        projectTag, meetingTag
    );
    await dbContext.SaveChangesAsync();

    Console.WriteLine("[3/5] Adding and saving todos...");
    dbContext.TodoItems.AddRange(
        todo1, todo2, todo3, todo4, todo5, todo6,
        todo7, todo8, todo9, todo10,
        todo11, todo12, todo13, todo14, todo15
    );
    await dbContext.SaveChangesAsync();

    await transaction.CommitAsync();
    Console.WriteLine("[4/5] All data saved successfully");
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

Console.WriteLine("[5/5] Verifying data...");
var userCount = await dbContext.Users.CountAsync();
var todoCount = await dbContext.TodoItems.CountAsync();
var tagCount = await dbContext.Tags.CountAsync();

Console.WriteLine($"\n✓ Successfully seeded database!");
Console.WriteLine($"  - Users: {userCount}");
Console.WriteLine($"  - Todos: {todoCount}");
Console.WriteLine($"  - Tags: {tagCount}");

Console.WriteLine($"\nSample Login Credentials:");
Console.WriteLine($"  alice@example.com   | Password123!");
Console.WriteLine($"  bob@example.com     | Password123!");
Console.WriteLine($"  charlie@example.com | Password123!");

Console.WriteLine("\nHello, World!");

// Explicitly dispose the context
await dbContext.DisposeAsync();
return 0;
