using Microsoft.Extensions.Logging.Abstractions;
using TaskManagement.Api.Dtos;
using TaskManagement.Api.Enums;
using TaskManagement.Api.Models;
using TaskManagement.Api.Services;
using Xunit;

namespace TaskManagement.Api.Tests;

public class TaskServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly TaskService _service;

    public TaskServiceTests()
    {
        _service = new TaskService(_db.Context, NullLogger<TaskService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private async Task<TaskResponse> CreateTaskAsync(
        int userId, string title = "Task", TaskPriority priority = TaskPriority.Medium)
    {
        var result = await _service.CreateAsync(
            new TaskCreateRequest { Title = title, Priority = priority }, userId, isPrivileged: false);
        return result.Value!;
    }

    private static TaskUpdateRequest UpdateTo(TaskResponse task, TaskItemStatus status) =>
        new() { Title = task.Title, Status = status, Priority = task.Priority };

    [Fact]
    public async Task Create_RegularUser_AssignsTaskToSelfEvenIfAnotherUserIsRequested()
    {
        var user = await _db.AddUserAsync("user@example.com");
        var other = await _db.AddUserAsync("other@example.com");

        var result = await _service.CreateAsync(
            new TaskCreateRequest { Title = "Task", AssignedToUserId = other.Id }, user.Id, isPrivileged: false);

        Assert.Equal(ServiceStatus.Success, result.Status);
        Assert.Equal(user.Id, result.Value!.AssignedToUserId);
    }

    [Fact]
    public async Task Create_Admin_CanAssignTaskToAnotherUser()
    {
        var admin = await _db.AddUserAsync("admin@example.com", UserRole.Admin);
        var user = await _db.AddUserAsync("user@example.com");

        var result = await _service.CreateAsync(
            new TaskCreateRequest { Title = "Task", AssignedToUserId = user.Id }, admin.Id, isPrivileged: true);

        Assert.Equal(ServiceStatus.Success, result.Status);
        Assert.Equal(user.Id, result.Value!.AssignedToUserId);
    }

    [Fact]
    public async Task Create_UnknownCategory_ReturnsInvalid()
    {
        var admin = await _db.AddUserAsync("admin@example.com", UserRole.Admin);

        var result = await _service.CreateAsync(
            new TaskCreateRequest { Title = "Task", CategoryId = 999 }, admin.Id, isPrivileged: true);

        Assert.Equal(ServiceStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task Create_UndefinedPriority_ReturnsInvalid()
    {
        var user = await _db.AddUserAsync("user@example.com");

        var result = await _service.CreateAsync(
            new TaskCreateRequest { Title = "Task", Priority = (TaskPriority)999 }, user.Id, isPrivileged: false);

        Assert.Equal(ServiceStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task GetTasks_RegularUserSeesOnlyOwnTasks_AdminSeesAll()
    {
        var admin = await _db.AddUserAsync("admin@example.com", UserRole.Admin);
        var user = await _db.AddUserAsync("user@example.com");
        var other = await _db.AddUserAsync("other@example.com");
        await CreateTaskAsync(user.Id, "Mine");
        await CreateTaskAsync(other.Id, "Theirs");

        var userTasks = await _service.GetTasksAsync(user.Id, false, null, null, null);
        var adminTasks = await _service.GetTasksAsync(admin.Id, true, null, null, null);

        Assert.Single(userTasks);
        Assert.Equal("Mine", userTasks[0].Title);
        Assert.Equal(2, adminTasks.Count);
    }

    [Fact]
    public async Task GetTasks_FilterByPriority_ReturnsOnlyMatchingTasks()
    {
        var user = await _db.AddUserAsync("user@example.com");
        await CreateTaskAsync(user.Id, "Urgent", TaskPriority.High);
        await CreateTaskAsync(user.Id, "Later", TaskPriority.Low);

        var tasks = await _service.GetTasksAsync(user.Id, false, null, TaskPriority.High, null);

        Assert.Single(tasks);
        Assert.Equal("Urgent", tasks[0].Title);
    }

    [Fact]
    public async Task GetById_OtherUsersTask_ReturnsNullForUserButAdminCanRead()
    {
        var admin = await _db.AddUserAsync("admin@example.com", UserRole.Admin);
        var user = await _db.AddUserAsync("user@example.com");
        var other = await _db.AddUserAsync("other@example.com");
        var task = await CreateTaskAsync(other.Id);

        Assert.Null(await _service.GetByIdAsync(task.Id, user.Id, isPrivileged: false));
        Assert.NotNull(await _service.GetByIdAsync(task.Id, admin.Id, isPrivileged: true));
    }

    [Fact]
    public async Task Update_OwnTask_ChangesStatus()
    {
        var user = await _db.AddUserAsync("user@example.com");
        var task = await CreateTaskAsync(user.Id);

        var result = await _service.UpdateAsync(
            task.Id, UpdateTo(task, TaskItemStatus.Completed), user.Id, isPrivileged: false);

        Assert.Equal(ServiceStatus.Success, result.Status);
        Assert.Equal(TaskItemStatus.Completed, result.Value!.Status);
    }

    [Fact]
    public async Task Update_RegularUser_CannotReassignTask()
    {
        var user = await _db.AddUserAsync("user@example.com");
        var other = await _db.AddUserAsync("other@example.com");
        var task = await CreateTaskAsync(user.Id);
        var request = UpdateTo(task, TaskItemStatus.InProgress);
        request.AssignedToUserId = other.Id;

        var result = await _service.UpdateAsync(task.Id, request, user.Id, isPrivileged: false);

        Assert.Equal(user.Id, result.Value!.AssignedToUserId);
    }

    [Fact]
    public async Task Update_OtherUsersTask_ReturnsNotFound()
    {
        var user = await _db.AddUserAsync("user@example.com");
        var other = await _db.AddUserAsync("other@example.com");
        var task = await CreateTaskAsync(other.Id);

        var result = await _service.UpdateAsync(
            task.Id, UpdateTo(task, TaskItemStatus.Completed), user.Id, isPrivileged: false);

        Assert.Equal(ServiceStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Update_UndefinedStatus_ReturnsInvalid()
    {
        var user = await _db.AddUserAsync("user@example.com");
        var task = await CreateTaskAsync(user.Id);

        var result = await _service.UpdateAsync(
            task.Id, UpdateTo(task, (TaskItemStatus)999), user.Id, isPrivileged: false);

        Assert.Equal(ServiceStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task Delete_OwnTask_RemovesIt()
    {
        var user = await _db.AddUserAsync("user@example.com");
        var task = await CreateTaskAsync(user.Id);

        var deleted = await _service.DeleteAsync(task.Id, user.Id, isPrivileged: false);

        Assert.True(deleted);
        Assert.Null(await _service.GetByIdAsync(task.Id, user.Id, isPrivileged: false));
    }

    [Fact]
    public async Task Delete_OtherUsersTask_ReturnsFalse()
    {
        var user = await _db.AddUserAsync("user@example.com");
        var other = await _db.AddUserAsync("other@example.com");
        var task = await CreateTaskAsync(other.Id);

        var deleted = await _service.DeleteAsync(task.Id, user.Id, isPrivileged: false);

        Assert.False(deleted);
    }

    [Fact]
    public async Task Dashboard_CountsTasksByStatus()
    {
        var admin = await _db.AddUserAsync("admin@example.com", UserRole.Admin);
        var user = await _db.AddUserAsync("user@example.com");
        var other = await _db.AddUserAsync("other@example.com");
        var pending = await CreateTaskAsync(user.Id, "Pending");
        var inProgress = await CreateTaskAsync(user.Id, "In progress");
        var completed = await CreateTaskAsync(user.Id, "Completed");
        await CreateTaskAsync(other.Id, "Other user's task");
        await _service.UpdateAsync(inProgress.Id, UpdateTo(inProgress, TaskItemStatus.InProgress), user.Id, false);
        await _service.UpdateAsync(completed.Id, UpdateTo(completed, TaskItemStatus.Completed), user.Id, false);

        var userDashboard = await _service.GetDashboardAsync(user.Id, isPrivileged: false);
        var adminDashboard = await _service.GetDashboardAsync(admin.Id, isPrivileged: true);

        Assert.Equal(1, userDashboard.Pending);
        Assert.Equal(1, userDashboard.InProgress);
        Assert.Equal(1, userDashboard.Completed);
        Assert.Equal(3, userDashboard.Total);
        Assert.Equal(4, adminDashboard.Total);
        Assert.Equal(2, adminDashboard.Pending);
        Assert.NotNull(pending);
    }
}