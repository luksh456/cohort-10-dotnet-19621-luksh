using Microsoft.EntityFrameworkCore;
using TaskManagement.Api.Data;
using TaskManagement.Api.Dtos;
using TaskManagement.Api.Enums;
using TaskManagement.Api.Models;

namespace TaskManagement.Api.Services;

public class TaskService : ITaskService
{
    private readonly AppDbContext _context;
    private readonly ILogger<TaskService> _logger;

    public TaskService(AppDbContext context, ILogger<TaskService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TaskResponse>> GetTasksAsync(
        int userId, bool isPrivileged, TaskItemStatus? status, TaskPriority? priority, int? categoryId)
    {
        IQueryable<TaskItem> query = _context.Tasks
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.AssignedToUser);

        if (!isPrivileged)
        {
            query = query.Where(t => t.AssignedToUserId == userId);
        }

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        if (priority.HasValue)
        {
            query = query.Where(t => t.Priority == priority.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(t => t.CategoryId == categoryId.Value);
        }

        var tasks = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
        return tasks.Select(ToResponse).ToList();
    }

    public async Task<TaskResponse?> GetByIdAsync(int id, int userId, bool isPrivileged)
    {
        var task = await _context.Tasks
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.AssignedToUser)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task is null || !CanAccess(task, userId, isPrivileged))
        {
            return null;
        }

        return ToResponse(task);
    }

    public async Task<ServiceResult<TaskResponse>> CreateAsync(
        TaskCreateRequest request, int userId, bool isPrivileged)
    {
        if (!Enum.IsDefined(request.Priority))
        {
            return ServiceResult<TaskResponse>.Invalid("Invalid priority.");
        }
        var assignedToUserId = isPrivileged ? request.AssignedToUserId ?? userId : userId;

        var error = await ValidateReferencesAsync(request.CategoryId, assignedToUserId);
        if (error is not null)
        {
            return ServiceResult<TaskResponse>.Invalid(error);
        }

        var task = new TaskItem
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Priority = request.Priority,
            DueDate = request.DueDate,
            CategoryId = request.CategoryId,
            AssignedToUserId = assignedToUserId
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();
                _logger.LogInformation("Task {TaskId} created by user {UserId}", task.Id, userId);

        var created = await GetByIdAsync(task.Id, userId, isPrivileged);
        return created is null
            ? ServiceResult<TaskResponse>.NotFound()
            : ServiceResult<TaskResponse>.Ok(created);
    }

    public async Task<ServiceResult<TaskResponse>> UpdateAsync(
        int id, TaskUpdateRequest request, int userId, bool isPrivileged)
    {
        if (!Enum.IsDefined(request.Status))
        {
            return ServiceResult<TaskResponse>.Invalid("Invalid status.");
        }

        if (!Enum.IsDefined(request.Priority))
        {
            return ServiceResult<TaskResponse>.Invalid("Invalid priority.");
        }
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null || !CanAccess(task, userId, isPrivileged))
        {
                _logger.LogWarning("User {UserId} tried to update missing or inaccessible task {TaskId}", userId, id);
            return ServiceResult<TaskResponse>.NotFound();
        }

        var assignedToUserId = isPrivileged ? request.AssignedToUserId : task.AssignedToUserId;

        var error = await ValidateReferencesAsync(request.CategoryId, assignedToUserId);
        if (error is not null)
        {
            return ServiceResult<TaskResponse>.Invalid(error);
        }

        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim();
        task.Status = request.Status;
        task.Priority = request.Priority;
        task.DueDate = request.DueDate;
        task.CategoryId = request.CategoryId;
        task.AssignedToUserId = assignedToUserId;

        await _context.SaveChangesAsync();
                _logger.LogInformation("Task {TaskId} updated by user {UserId}", task.Id, userId);

        var updated = await GetByIdAsync(task.Id, userId, isPrivileged);
        return updated is null
            ? ServiceResult<TaskResponse>.NotFound()
            : ServiceResult<TaskResponse>.Ok(updated);
    }

    public async Task<bool> DeleteAsync(int id, int userId, bool isPrivileged)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null || !CanAccess(task, userId, isPrivileged))
        {
                     _logger.LogWarning("User {UserId} tried to delete missing or inaccessible task {TaskId}", userId, id);
            return false;
        }

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync();
                _logger.LogInformation("Task {TaskId} deleted by user {UserId}", id, userId);
        return true;
    }

    public async Task<DashboardResponse> GetDashboardAsync(int userId, bool isPrivileged)
    {
        IQueryable<TaskItem> query = _context.Tasks.AsNoTracking();

        if (!isPrivileged)
        {
            query = query.Where(t => t.AssignedToUserId == userId);
        }

        var counts = await query
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        int CountFor(TaskItemStatus status) => counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;

        return new DashboardResponse
        {
            Completed = CountFor(TaskItemStatus.Completed),
            InProgress = CountFor(TaskItemStatus.InProgress),
            Pending = CountFor(TaskItemStatus.Pending),
            Total = counts.Sum(c => c.Count)
        };
    }

    private static bool CanAccess(TaskItem task, int userId, bool isPrivileged)
    {
        return isPrivileged || task.AssignedToUserId == userId;
    }

    private async Task<string?> ValidateReferencesAsync(int? categoryId, int? assignedToUserId)
    {
        if (categoryId.HasValue && !await _context.Categories.AnyAsync(c => c.Id == categoryId.Value))
        {
            return "Category does not exist.";
        }

        if (assignedToUserId.HasValue && !await _context.Users.AnyAsync(u => u.Id == assignedToUserId.Value))
        {
            return "Assigned user does not exist.";
        }

        return null;
    }

    private static TaskResponse ToResponse(TaskItem task)
    {
        return new TaskResponse
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            Priority = task.Priority,
            DueDate = task.DueDate,
            CreatedAt = task.CreatedAt,
            CategoryId = task.CategoryId,
            CategoryName = task.Category?.Name,
            AssignedToUserId = task.AssignedToUserId,
            AssignedToUserName = task.AssignedToUser?.Name
        };
    }
}