using Galileo.Chat.Domain.Abstractions;
using Galileo.Chat.Domain.UseCases.Auth;
using Galileo.Chat.Infrastructure.Auth;
using Galileo.Chat.Infrastructure.Options;
using Galileo.Chat.Infrastructure.Persistence;
using Galileo.Chat.Infrastructure.Persistence.Repositories;
using Galileo.Chat.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Galileo.Chat.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<PersistenceOptions>()
            .Bind(configuration.GetSection(PersistenceOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();

        services.AddDbContext<ChatDbContext>((sp, opt) =>
        {
            var persistence = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PersistenceOptions>>().Value;
            opt.UseSqlite(persistence.ConnectionString,
                sqlite => sqlite.MigrationsAssembly(typeof(ChatDbContext).Assembly.FullName));
        });

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();

        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddScoped<LoginHandler>();
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<Galileo.Chat.Domain.UseCases.Rooms.CreateRoomHandler>();
        services.AddScoped<Galileo.Chat.Domain.UseCases.Rooms.GetRoomByNameHandler>();
        services.AddScoped<Galileo.Chat.Domain.Abstractions.ListRoomsHandler>();

        services.AddScoped<DatabaseInitializer>();

        return services;
    }
}
