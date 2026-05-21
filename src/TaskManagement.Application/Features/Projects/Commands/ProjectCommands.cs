using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common.Exceptions;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Common.Models;
using TaskManagement.Application.Features.Projects.Queries;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Features.Projects.Commands;

// ── Create Project
public record CreateProjectCommand(string Name, string Description) : IRequest<ApiResponse<ProjectDto>>;

public class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, ApiResponse<ProjectDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public CreateProjectCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, ICacheService cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<ApiResponse<ProjectDto>> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        var project = new Project
        {
            Name = request.Name,
            Description = request.Description,
            UserId = _currentUser.UserId!.Value
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByPatternAsync($"projects:user:{_currentUser.UserId}:*", cancellationToken);

        return ApiResponse<ProjectDto>.SuccessResult(
            new ProjectDto(project.Id, project.Name, project.Description, project.CreatedAt, 0),
            "Project created successfully", 201);
    }
}

// ── Update Project 

public record UpdateProjectCommand(Guid Id, string Name, string Description) : IRequest<ApiResponse<ProjectDto>>;

public class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public class UpdateProjectCommandHandler : IRequestHandler<UpdateProjectCommand, ApiResponse<ProjectDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public UpdateProjectCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, ICacheService cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<ApiResponse<ProjectDto>> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.UserId == _currentUser.UserId!.Value, cancellationToken)
            ?? throw new NotFoundException(nameof(Project), request.Id);

        project.Name = request.Name;
        project.Description = request.Description;
        project.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByPatternAsync($"projects:user:{_currentUser.UserId}:*", cancellationToken);

        return ApiResponse<ProjectDto>.SuccessResult(
            new ProjectDto(project.Id, project.Name, project.Description, project.CreatedAt, project.Tasks.Count));
    }
}

// ── Delete Project 
public record DeleteProjectCommand(Guid Id) : IRequest<ApiResponse<bool>>;

public class DeleteProjectCommandHandler : IRequestHandler<DeleteProjectCommand, ApiResponse<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public DeleteProjectCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, ICacheService cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<ApiResponse<bool>> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.UserId == _currentUser.UserId!.Value, cancellationToken)
            ?? throw new NotFoundException(nameof(Project), request.Id);

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByPatternAsync($"projects:user:{_currentUser.UserId}:*", cancellationToken);

        return ApiResponse<bool>.SuccessResult(true, "Project deleted successfully");
    }
}
