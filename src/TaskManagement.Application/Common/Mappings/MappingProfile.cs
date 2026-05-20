using AutoMapper;
using TaskManagement.Application.Features.Auth.Commands;
using TaskManagement.Application.Features.Projects.Commands;
using TaskManagement.Application.Features.Projects.Queries;
using TaskManagement.Application.Features.Tasks.Commands;
using TaskManagement.Application.Features.Tasks.Queries;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Auth
        CreateMap<RegisterCommand, User>()
            .ForMember(d => d.PasswordHash, opt => opt.Ignore());

        // Projects
        CreateMap<CreateProjectCommand, Project>();
        CreateMap<Project, ProjectDto>()
            .ForMember(d => d.TaskCount, opt => opt.MapFrom(s => s.Tasks.Count));

        // Tasks
        CreateMap<CreateTaskCommand, ProjectTask>();
        CreateMap<ProjectTask, TaskDto>();
    }
}
