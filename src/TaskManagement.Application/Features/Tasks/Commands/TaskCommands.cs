using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common.Exceptions;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Common.Models;
using TaskManagement.Application.Features.Tasks.Queries;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskStatus = TaskManagement.Domain.Enums.TaskStatus;

namespace TaskManagement.Application.Features.Tasks.Commands;

// ── Create Task 

public record CreateTaskCommand(
    string Title,
    string Description,
    TaskPriority Priority,
    DateTime? DueDate,
    Guid ProjectId
) : IRequest<ApiResponse<TaskDto>>;

public class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.DueDate).GreaterThan(DateTime.UtcNow)
            .When(x => x.DueDate.HasValue)
            .WithMessage("Due date must be in the future.");
    }
}

public class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, ApiResponse<TaskDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public CreateTaskCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, ICacheService cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<ApiResponse<TaskDto>> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        var projectExists = await _context.Projects
            .AnyAsync(p => p.Id == request.ProjectId && p.UserId == _currentUser.UserId!.Value, cancellationToken);

        if (!projectExists)
            throw new NotFoundException(nameof(Project), request.ProjectId);

        var task = new ProjectTask
        {
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            DueDate = request.DueDate,
            ProjectId = request.ProjectId,
            Status = TaskStatus.Todo
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByPatternAsync($"tasks:project:{request.ProjectId}:*", cancellationToken);

        return ApiResponse<TaskDto>.SuccessResult(
            new TaskDto(task.Id, task.Title, task.Description, task.Status.ToString(),
                task.Priority.ToString(), task.DueDate, task.ProjectId, task.CreatedAt),
            "Task created successfully", 201);
    }
}

// ── Update Task Status 

public record UpdateTaskStatusCommand(Guid Id, TaskStatus Status) : IRequest<ApiResponse<TaskDto>>;

public class UpdateTaskStatusCommandValidator : AbstractValidator<UpdateTaskStatusCommand>
{
    public UpdateTaskStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class UpdateTaskStatusCommandHandler : IRequestHandler<UpdateTaskStatusCommand, ApiResponse<TaskDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public UpdateTaskStatusCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, ICacheService cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<ApiResponse<TaskDto>> Handle(UpdateTaskStatusCommand request, CancellationToken cancellationToken)
    {
        var task = await _context.Tasks
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.Project.UserId == _currentUser.UserId!.Value, cancellationToken)
            ?? throw new NotFoundException(nameof(ProjectTask), request.Id);

        task.Status = request.Status;
        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByPatternAsync($"tasks:project:{task.ProjectId}:*", cancellationToken);

        return ApiResponse<TaskDto>.SuccessResult(
            new TaskDto(task.Id, task.Title, task.Description, task.Status.ToString(),
                task.Priority.ToString(), task.DueDate, task.ProjectId, task.CreatedAt));
    }
}

// ── Update Task (Full Entity) 

public record UpdateTaskCommand(
    Guid Id,
    string Title,
    string Description,
    TaskStatus Status,
    TaskPriority Priority,
    DateTime? DueDate
) : IRequest<ApiResponse<TaskDto>>;

public class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public class UpdateTaskCommandHandler : IRequestHandler<UpdateTaskCommand, ApiResponse<TaskDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public UpdateTaskCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, ICacheService cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<ApiResponse<TaskDto>> Handle(UpdateTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _context.Tasks
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.Project.UserId == _currentUser.UserId!.Value, cancellationToken)
            ?? throw new NotFoundException(nameof(ProjectTask), request.Id);

        task.Title = request.Title;
        task.Description = request.Description;
        task.Status = request.Status;
        task.Priority = request.Priority;
        task.DueDate = request.DueDate;
        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByPatternAsync($"tasks:project:{task.ProjectId}:*", cancellationToken);

        return ApiResponse<TaskDto>.SuccessResult(
            new TaskDto(task.Id, task.Title, task.Description, task.Status.ToString(),
                task.Priority.ToString(), task.DueDate, task.ProjectId, task.CreatedAt));
    }
}

// ── Delete Task 

public record DeleteTaskCommand(Guid Id) : IRequest<ApiResponse<bool>>;

public class DeleteTaskCommandHandler : IRequestHandler<DeleteTaskCommand, ApiResponse<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public DeleteTaskCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, ICacheService cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<ApiResponse<bool>> Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _context.Tasks
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.Project.UserId == _currentUser.UserId!.Value, cancellationToken)
            ?? throw new NotFoundException(nameof(ProjectTask), request.Id);

        var projectId = task.ProjectId;
        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByPatternAsync($"tasks:project:{projectId}:*", cancellationToken);

        return ApiResponse<bool>.SuccessResult(true, "Task deleted successfully");
    }
}
