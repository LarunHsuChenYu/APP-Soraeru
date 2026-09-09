using Microsoft.Extensions.DependencyInjection;
using Soraeru.Application.Analyze;
using Soraeru.Application.Auth;
using Soraeru.Application.Curator;
using Soraeru.Application.Notebook;
using Soraeru.Application.Quota;

namespace Soraeru.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMeService, MeService>();
        services.AddScoped<IQuotaService, QuotaService>();
        services.AddScoped<IAnalyzeWordService, AnalyzeWordService>();
        services.AddScoped<INotebookService, NotebookService>();
        services.AddScoped<ICuratorMnemonicService, CuratorMnemonicService>();
        services.AddScoped<ILlmAdminService, LlmAdminService>();
        services.AddScoped<ICuratorUserAdminService, CuratorUserAdminService>();
        return services;
    }
}
