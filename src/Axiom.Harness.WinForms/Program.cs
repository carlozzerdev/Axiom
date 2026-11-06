using Axiom.Harness.Application;
using Axiom.Harness.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Axiom.Harness.WinForms;

internal static class Program
{
    [STAThread]
    private static async Task Main()
    {
        ApplicationConfiguration.Initialize();
        var configuration = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json", optional: false).Build();
        var services = new ServiceCollection();
        services.AddDbContextFactory<HarnessDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("Harness")));
        services.AddSingleton<IAgentRunRepository, EfAgentRunRepository>();
        services.AddSingleton<IAgentRunner, AgentRunner>();
        services.AddSingleton<IAgentTool, FileReadTool>();
        services.AddSingleton<IAgentTool, ListFilesTool>();
        services.AddHttpClient<IAgentTool, HttpGetTool>(client => client.Timeout = TimeSpan.FromSeconds(60));
        services.AddSingleton<IAgentToolCatalog, AgentToolCatalog>();
        services.AddHttpClient<IModelClient, OllamaClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Ollama:BaseUrl"] ?? "http://localhost:11434");
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.AddTransient<MainForm>();
        await using var provider = services.BuildServiceProvider();
        try
        {
            await using var db = await provider.GetRequiredService<IDbContextFactory<HarnessDbContext>>().CreateDbContextAsync();
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo preparar SQL Server. Revise appsettings.json y los permisos.\n\n{ex.Message}", "Axiom Harness", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        System.Windows.Forms.Application.Run(provider.GetRequiredService<MainForm>());
    }
}
