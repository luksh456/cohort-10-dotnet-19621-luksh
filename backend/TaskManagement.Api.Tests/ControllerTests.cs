using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Api.Controllers;
using TaskManagement.Api.Dtos;
using TaskManagement.Api.Enums;
using TaskManagement.Api.Services;
using Xunit;

namespace TaskManagement.Api.Tests;

public class ControllerTests
{
    private sealed class FakeAuthService : IAuthService
    {
        public bool RegisterResult { get; set; }
        public AuthResponse? LoginResult { get; set; }

        public Task<bool> RegisterAsync(RegisterRequest request) => Task.FromResult(RegisterResult);
        public Task<AuthResponse?> LoginAsync(LoginRequest request) => Task.FromResult(LoginResult);
    }

    private sealed class FakeTaskService : ITaskService
    {
        public TaskResponse? ByIdResult { get; set; }

        public Task<IReadOnlyList<TaskResponse>> GetTasksAsync(
            int userId, bool isPrivileged, TaskItemStatus? status, TaskPriority? priority, int? categoryId) =>
            Task.FromResult<IReadOnlyList<TaskResponse>>(Array.Empty<TaskResponse>());

        public Task<TaskResponse?> GetByIdAsync(int id, int userId, bool isPrivileged) =>
            Task.FromResult(ByIdResult);

        public Task<ServiceResult<TaskResponse>> CreateAsync(TaskCreateRequest request, int userId, bool isPrivileged) =>
            Task.FromResult(ServiceResult<TaskResponse>.Invalid("Invalid priority."));

        public Task<ServiceResult<TaskResponse>> UpdateAsync(
            int id, TaskUpdateRequest request, int userId, bool isPrivileged) =>
            Task.FromResult(ServiceResult<TaskResponse>.NotFound());

        public Task<bool> DeleteAsync(int id, int userId, bool isPrivileged) => Task.FromResult(false);

        public Task<DashboardResponse> GetDashboardAsync(int userId, bool isPrivileged) =>
            Task.FromResult(new DashboardResponse());
    }

    private static TasksController CreateTasksController(FakeTaskService service, params Claim[] claims)
    {
        return new TasksController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                }
            }
        };
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Register_ReturnsAcceptedForNewAndExistingEmails(bool created)
    {
        var controller = new AuthController(new FakeAuthService { RegisterResult = created });

        var result = await controller.Register(new RegisterRequest { Name = "Ali", Email = "ali@example.com", Password = "Password1!" });

        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        var controller = new AuthController(new FakeAuthService { LoginResult = null });

        var result = await controller.Login(new LoginRequest { Email = "ali@example.com", Password = "wrong" });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOk()
    {
        var controller = new AuthController(new FakeAuthService { LoginResult = new AuthResponse { Token = "token" } });

        var result = await controller.Login(new LoginRequest { Email = "ali@example.com", Password = "Password1!" });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetTaskById_MissingTask_ReturnsNotFound()
    {
        var controller = CreateTasksController(
            new FakeTaskService(), new Claim(ClaimTypes.NameIdentifier, "1"));

        var result = await controller.GetById(5);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetTaskById_WithoutUserClaim_ReturnsUnauthorized()
    {
        var controller = CreateTasksController(new FakeTaskService());

        var result = await controller.GetById(5);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task CreateTask_InvalidRequest_ReturnsBadRequest()
    {
        var controller = CreateTasksController(
            new FakeTaskService(), new Claim(ClaimTypes.NameIdentifier, "1"));

        var result = await controller.Create(new TaskCreateRequest { Title = "Task" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void AdminUsersController_RequiresAdminRole()
    {
        var attribute = typeof(AdminUsersController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("Admin", attribute!.Roles);
    }

    [Fact]
    public void CategoriesController_CreateRequiresAdminRole()
    {
        var method = typeof(CategoriesController).GetMethod(nameof(CategoriesController.Create));
        var attribute = method!.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("Admin", attribute!.Roles);
    }
}