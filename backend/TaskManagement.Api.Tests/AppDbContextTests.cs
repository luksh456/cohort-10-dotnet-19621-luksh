using Microsoft.EntityFrameworkCore;
using TaskManagement.Api.Models;
using Xunit;

namespace TaskManagement.Api.Tests;

public class AppDbContextTests
{
    [Fact]
    public async Task Users_WithSameEmail_ViolateUniqueIndex()
    {
        using var db = new TestDatabase();
        await db.AddUserAsync("dup@example.com");
        db.Context.Users.Add(new User { Name = "Other", Email = "dup@example.com", PasswordHash = "hash" });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.Context.SaveChangesAsync());
    }
}