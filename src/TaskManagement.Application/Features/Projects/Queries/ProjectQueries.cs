using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common.Exceptions;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Common.Models;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Features.Projects.Queries;

// ── DTOs 

public record ProjectDto(
    Guid Id,
    string Name,
    string Description,
    DateTime CreatedAt,
    int TaskCount
);

// ── Get All Projects 

public record GetAllProjectsQuery(int Page = 1, int PageSize = 10)
    : IRequest<ApiResponse<PagedResult<ProjectDto>>>;

public class GetAllProjectsQueryHandler
    : IRequestHandler<GetAllProjectsQuery, ApiResponse<PagedResult<ProjectDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public GetAllProjectsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser, ICacheService cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<ApiResponse<PagedResult<ProjectDto>>> Handle(GetAllProjectsQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"projects:user:{_currentUser.UserId}:page:{request.Page}:size:{request.PageSize}";
        var cached = await _cache.GetAsync<PagedResult<ProjectDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return ApiResponse<PagedResult<ProjectDto>>.SuccessResult(cached);

        var query = _context.Projects
            .Include(p => p.Tasks)
            .Where(p => p.UserId == _currentUser.UserId!.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new ProjectDto(p.Id, p.Name, p.Description, p.CreatedAt, p.Tasks.Count))
            .ToListAsync(cancellationToken);

        var result = new PagedResult<ProjectDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);

        return ApiResponse<PagedResult<ProjectDto>>.SuccessResult(result);
    }
}

// ── Get Project By Id 
public record GetProjectByIdQuery(Guid Id) : IRequest<ApiResponse<ProjectDto>>;

public class GetProjectByIdQueryHandler : IRequestHandler<GetProjectByIdQuery, ApiResponse<ProjectDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetProjectByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<ProjectDto>> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.UserId == _currentUser.UserId!.Value, cancellationToken)
            ?? throw new NotFoundException(nameof(Project), request.Id);

        return ApiResponse<ProjectDto>.SuccessResult(
            new ProjectDto(project.Id, project.Name, project.Description, project.CreatedAt, project.Tasks.Count));
    }
}
