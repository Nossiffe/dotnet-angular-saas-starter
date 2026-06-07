# ADR 0001 — Strongly-typed IDs & value objects mono-valeur via Vogen

- **Statut** : Accepté (2026-06-07)
- **Contexte** : élévation DDD du backend (agrégats riches, value objects, strongly-typed IDs).

## Décision

On utilise **[Vogen](https://github.com/SteveDunn/Vogen)** (`8.0.5`) comme **source-generator** pour :
- les **strongly-typed IDs** : `ProductId`, `OrganizationId` (`[ValueObject<Guid>]`) ;
- les **value objects mono-valeur** : `Slug` (`[ValueObject<string>]`, avec normalisation + validation).

Les value objects **multi-champs** (ex. `Money` = montant + devise) ne passent **pas** par Vogen : ce sont des `record` C# (égalité par valeur) mappés en **owned type** EF.

## Pourquoi pas l'intégration EF de Vogen ?

Vogen sait générer un `EfCoreValueConverter`, **mais ce code généré référence `Microsoft.EntityFrameworkCore` dans l'assembly où vit le type** — donc **dans `Domain`**. Or `Domain` doit rester **pur** (aucune dépendance EF — c'est vérifié par le test d'architecture `NetArchTest`).

**Conséquence (convention du repo)** : les types Vogen sont générés en **`Conversions.SystemTextJson` uniquement** (pour la sérialisation API), et la **conversion de stockage EF est écrite à la main dans `Infrastructure`**.

On ajoute aussi `comparison: ComparisonGeneration.Omit` (sinon Vogen émet `IComparable` sans les opérateurs de comparaison → l'analyseur Sonar/Meziantou casse le build).

## Conséquences

- `Domain` ne dépend que de `Vogen` (analyzer/abstractions), pas d'EF → archi test vert.
- Le **wire reste primitif** : les DTO/commands/route params utilisent `Guid`/`decimal`. Les IDs typés sont **internes au domaine** ; on convertit à la frontière des handlers (`ProductId.From(guid)`, `id.Value`).
- Un peu de boilerplate manuel par ID (un converter EF + un enregistrement). **À templatiser dans `new-saas`** et à rappeler dans `.claude/rules/dotnet.md`.

## Recette : ajouter un nouveau strongly-typed ID `FooId`

1. **Domain** — `src/Domain/<Feature>/FooId.cs` :
   ```csharp
   using Vogen;
   namespace Saas.Domain.<Feature>;

   [ValueObject<Guid>(conversions: Conversions.SystemTextJson, comparison: ComparisonGeneration.Omit)]
   public partial struct FooId;
   ```
2. **Infrastructure** — `src/Infrastructure/Persistence/Converters/FooIdConverter.cs` :
   ```csharp
   using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
   using Saas.Domain.<Feature>;
   namespace Saas.Infrastructure.Persistence.Converters;

   public sealed class FooIdConverter() : ValueConverter<FooId, Guid>(
       id => id.Value,
       value => FooId.From(value));
   ```
3. **Infrastructure** — l'enregistrer dans `AppDbContext.ConfigureConventions` :
   ```csharp
   configurationBuilder.Properties<FooId>().HaveConversion<FooIdConverter>();
   ```
4. Utiliser `FooId` comme `Id` de l'agrégat (`AggregateRoot<FooId>`), convertir au bord des handlers (`FooId.From(guid)` / `id.Value`).

> Même schéma pour un value object mono-valeur (`[ValueObject<string>]`) avec une méthode de validation `Validate` (Vogen) ; le converter EF cible le type sous-jacent (`ValueConverter<Foo, string>`).

## Alternatives écartées

- **Intégration EF de Vogen** : ferait fuiter EF dans `Domain` (casse l'archi). Écartée.
- **StronglyTypedId (Andrew Lock)** : valable, mais Vogen couvre aussi les value objects mono-valeur (validation) → un seul outil.
- **Hand-roll complet** (`readonly record struct` + converters + `IParsable` + JSON à la main) : trop de boilerplate répétitif et fragile.
