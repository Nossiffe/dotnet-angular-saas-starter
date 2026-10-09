using System.Text.Json;

namespace Saas.Infrastructure.Persistence.Outbox;

// Options STJ partagées entre l'écriture (DomainEventsInterceptor) et la lecture (OutboxProcessor).
// Les domain events sont des `record` à ctor paramétré : STJ les supporte nativement (mapping
// par nom de propriété sur le ctor). Vogen génère un converter SystemTextJson pour les IDs typés
// (ProductId, etc.), donc pas besoin de converters supplémentaires ici.
internal static class OutboxJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        // Stable et explicite : on sérialise sur le type concret de l'event au write,
        // et on désérialise sur le Type résolu au read.
        WriteIndented = false,
    };
}
