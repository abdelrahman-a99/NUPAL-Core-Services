using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using NUPAL.Core.Application.Interfaces;
using Nupal.Core.Infrastructure.Repositories;
using Nupal.Core.Infrastructure.Services;

namespace NUPAL.Core.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            var mongoUrl = configuration.GetValue<string>("MONGO_URL")
                           ?? Environment.GetEnvironmentVariable("MONGO_URL")
                           ?? throw new InvalidOperationException("MongoDB connection string is not configured. Please provide 'MONGO_URL' in appsettings or environment variables.");

            services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoUrl));
            services.AddSingleton<IMongoDatabase>(sp =>
            {
                var client = sp.GetRequiredService<IMongoClient>();
                return client.GetDatabase("nupal");
            });

            services.AddScoped<IStudentRepository, StudentRepository>();
            services.AddScoped<IContactRepository, ContactRepository>();

            services.AddScoped<IChatConversationRepository, ChatConversationRepository>();
            services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
            services.AddHttpClient<IAgentClient, AgentClient>();

            services.AddScoped<IRlJobRepository, RlJobRepository>();
            services.AddScoped<IRlRecommendationRepository, RlRecommendationRepository>();
            services.AddHttpClient<IRlService, RlService>();
            services.AddScoped<IPrecomputeService, PrecomputeService>();

            // Register Wuzzuf job scraping service
            services.AddHttpClient<IJobService, Services.WuzzufJobService>();
            
            services.AddScoped<IDynamicSkillsService, Services.DynamicSkillsService>();

            // Register background worker for automatic sync
            services.AddHostedService<PrecomputeBackgroundWorker>();

            return services;
        }
    }
}
