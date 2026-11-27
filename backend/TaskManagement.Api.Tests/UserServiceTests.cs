using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Api.Dtos;
using TaskManagement.Api.Enums;
using TaskManagement.Api.Models;
using TaskManagement.Api.Services;
using Xunit;

namespace TaskManagement.Api.Tests;

public class UserServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly UserService _service;

    public UserServiceTests()
    {
        _service = new UserService(_db.Context, new PasswordHasher<User>());
    }

    public void Dispose() => _db.Dispose();

    private static CreateUserRequest NewUser(string email = "new@example.com", UserRole role = UserRole.Admin) =>
        new() { Name = "New", Email = email, Password = "Password1!", Role = role };

    [Fact]
    public async Task CreateUser_ValidRequest_CreatesUserWithRequestedRoleAndHashedPassword()
    {
        var result = await _service.CreateUserAsync(NewUser());

        Assert.Equal(ServiceStatus.Success, result.Status);
        Assert.Equal(UserRole.Admin, result.Value!.Role);
        var saved = await _db.Context.Users.SingleAsync();
        Assert.NotEqual("Password1!", saved.PasswordHash);
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_ReturnsInvalid()
    {
        await _service.CreateUserAsync(NewUser());

        var result = await _service.CreateUserAsync(NewUser("NEW@example.com"));

        Assert.Equal(ServiceStatus.Invalid, result.Status);
        Assert.Equal(1, await _db.Context.Users.CountAsync());
    }

    [Fact]
    public async Task CreateUser_UndefinedRole_ReturnsInvalid()
    {
        var result = await _service.CreateUserAsync(NewUser(role: (UserRole)99));

        Assert.Equal(ServiceStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task GetUsers_ReturnsAllUsers()
    {
        await _db.AddUserAsync("a@example.com");
        await _db.AddUserAsync("b@example.com");

        var users = await _service.GetUsersAsync();

        Assert.Equal(2, users.Count);
    }
}