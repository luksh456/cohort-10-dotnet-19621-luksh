using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Api.Dtos;
using TaskManagement.Api.Enums;
using TaskManagement.Api.Models;
using TaskManagement.Api.Services;
using TaskManagement.Api.Settings;
using Xunit;

namespace TaskManagement.Api.Tests;

public class AuthServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        var jwtSettings = new JwtSettings
        {
            Key = new string('k', 64),
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpiryMinutes = 60
        };

        _service = new AuthService(_db.Context, new PasswordHasher<User>(), jwtSettings);
    }

    public void Dispose() => _db.Dispose();

    private static RegisterRequest NewRegistration(string email = "ali@example.com") =>
        new() { Name = "Ali", Email = email, Password = "Password1!" };

    [Fact]
    public async Task Register_NewEmail_CreatesUserWithUserRoleAndHashedPassword()
    {
        var created = await _service.RegisterAsync(NewRegistration("Ali@Example.com"));

        Assert.True(created);
        var user = await _db.Context.Users.SingleAsync();
        Assert.Equal("ali@example.com", user.Email);
        Assert.Equal(UserRole.User, user.Role);
        Assert.NotEqual("Password1!", user.PasswordHash);
    }

    [Fact]
    public async Task Register_ExistingEmail_ReturnsFalseAndDoesNotCreateDuplicate()
    {
        await _service.RegisterAsync(NewRegistration());

        var created = await _service.RegisterAsync(NewRegistration("ALI@example.com"));

        Assert.False(created);
        Assert.Equal(1, await _db.Context.Users.CountAsync());
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenAndUserDetails()
    {
        await _service.RegisterAsync(NewRegistration());

        var response = await _service.LoginAsync(new LoginRequest { Email = "ALI@example.com", Password = "Password1!" });

        Assert.NotNull(response);
        Assert.False(string.IsNullOrWhiteSpace(response!.Token));
        Assert.Equal("ali@example.com", response.Email);
        Assert.Equal("User", response.Role);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsNull()
    {
        await _service.RegisterAsync(NewRegistration());

        var response = await _service.LoginAsync(new LoginRequest { Email = "ali@example.com", Password = "wrong" });

        Assert.Null(response);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsNull()
    {
        var response = await _service.LoginAsync(new LoginRequest { Email = "nobody@example.com", Password = "Password1!" });

        Assert.Null(response);
    }
}