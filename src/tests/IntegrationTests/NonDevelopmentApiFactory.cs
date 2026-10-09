using Microsoft.AspNetCore.Hosting;

namespace Saas.IntegrationTests;

/// <summary>
/// Force un environnement non-Development pour l'host de test. <see cref="PostgresApiFactory"/>
/// (via <c>WebApplicationFactory&lt;Program&gt;</c>) tourne en Development par défaut — c'est
/// précisément l'environnement où <c>HangfireDashboardAuthFilter</c> autorise le loopback SANS
/// authentification (confort dev). Comme les requêtes du client de test arrivent elles-mêmes en
/// loopback, un test naïf du dashboard passerait de façon vacuously vraie
/// (200 obtenu via la dérogation dev, pas via une vraie auth). Cette factory bascule l'host sur
/// "Production" pour exercer la branche prod du filtre (claim <c>platform_role=PlatformAdmin</c>
/// exigé) SANS toucher au filtre lui-même — le contrat de prod reste inchangé et non affaibli.
/// </summary>
public sealed class NonDevelopmentApiFactory : PostgresApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        // Hors Development, appsettings.Development.json n'est pas lu : ces réglages sont obligatoires,
        // comme en production.
        builder.UseSetting("App:BaseUrl", "https://app.example.test");
        builder.UseSetting("Email:Host", "localhost");
        builder.UseSetting("Email:Port", "1025");
        builder.UseSetting("Email:FromAddress", "no-reply@example.test");
        base.ConfigureWebHost(builder);
    }
}
