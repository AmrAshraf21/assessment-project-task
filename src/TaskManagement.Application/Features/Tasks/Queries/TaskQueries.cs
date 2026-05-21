using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common.Exceptions;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Common.Models;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.Features.Tasks.Queries;

// ── DTOs 

public record TaskDto(
    Guid Id,
    string Title,
    string Description,
    string Status,
    string Priority,
    DateTime? DueDate,
    Guid ProjectId,
    DateTime CreatedAt
);

// ── Get Tasks By Project 

public record GetTasksByProjectQuery(
    Guid ProjectId,
    int Page = 1,
    int PageSize = 10,
    string? StatusFilter = null
) : IRequest<ApiResponse<PagedResult<TaskDto>>>;

public class GetTasksByProjectQueryHandler
    : IRequestHandler<GetTasksByProjectQuery, ApiResponse<PagedResult<TaskDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public GetTasksByProjectQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser, ICacheService cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<ApiResponse<PagedResult<TaskDto>>> Handle(GetTasksByProjectQuery request, CancellationToken cancellationToken)
    {
        var projectExists = await _context.Projects
            .AnyAsync(p => p.Id == request.ProjectId && p.UserId == _currentUser.UserId!.Value, cancellationToken);

        if (!projectExists)
            throw new NotFoundException(nameof(Project), request.ProjectId);

        var cacheKey = $"tasks:project:{request.ProjectId}:page:{request.Page}:size:{request.PageSize}:status:{request.StatusFilter}";
        var cached = await _cache.GetAsync<PagedResult<TaskDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return ApiResponse<PagedResult<TaskDto>>.SuccessResult(cached);

        var query = _context.Tasks.Where(t => t.ProjectId == request.ProjectId);

        if (!string.IsNullOrWhiteSpace(request.StatusFilter) &&
            Enum.TryParse<Domain.Enums.TaskStatus>(request.StatusFilter, true, out var status))
        {
            query = query.Where(t => t.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new TaskDto(
                t.Id, t.Title, t.Description,
                t.Status.ToString(), t.Priority.ToString(),
                t.DueDate, t.ProjectId, t.CreatedAt))
            .ToListAsync(cancellationToken);

        var result = new PagedResult<TaskDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2), cancellationToken);
        return ApiResponse<PagedResult<TaskDto>>.SuccessResult(result);
    }
}
