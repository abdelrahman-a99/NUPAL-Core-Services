using Microsoft.Extensions.DependencyInjection;
using NUPAL.Core.Application.Interfaces;
using NUPAL.Core.Application.Services;

namespace NUPAL.Core.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IStudentService, StudentService>();
            return services;
        }
    }
}
