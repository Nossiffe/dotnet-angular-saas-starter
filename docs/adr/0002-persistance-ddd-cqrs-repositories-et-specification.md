# ADR 0002 — Persistance DDD/CQRS : repositories par agrégat (write) + Dapper (read), Ardalis.Specification écarté pour l'instant

- **Statut** : Accepté (2026-06-07)
- **Contexte** : élévation DDD du backend. Question : comment persister les agrégats sans retomber dans un anti-pattern, et faut-il adopter [Ardalis.Specification](https://specification.ardalis.com/) ?

## Décision

Split **CQRS** au niveau de la persistance (approche Microsoft eShopOnWeb / Ardalis) :

| | Write (commands) | Read (queries) |
|---|---|---|
| **Mécanisme** | **Un repository par aggregate root** (`IProductRepository`, `IOrganizationRepository`) + `IUnitOfWork` | **Dapper** (`IDbConnectionFactory`), projections SQL directes → DTO |
| **Rôle** | Charge/sauve l'agrégat **entier** ; frontière d'agrégat ; domaine pur | Flexibilité totale (joins, pas de contrainte d'agrégat), perf |

- **Pas** de `IRepository<T>` **générique** exposant `IQueryable` (ça, c'est l'anti-pattern redondant avec EF).
- **Pas** d'exposition de `DbSet` à l'Application (l'ancien `IAppDbContext` a été supprimé). Les `DbSet` sont `internal` à Infrastructure, utilisés par les repositories.
- `IUnitOfWork.SaveChangesAsync` / `ExecuteInTransactionAsync` portent le commit / la transaction (execution strategy EF avec `EnableRetryOnFailure`).

## Ardalis.Specification — évalué, **écarté pour l'instant**

`Ardalis.Specification` encapsule la logique de requête dans des classes `Specification` réutilisables, utilisées avec un repository (souvent le générique `IRepositoryBase<T>`). Très adopté ([projects-using-specification](https://specification.ardalis.com/projects-using-specification/) : eShopOnWeb, le template Clean Architecture d'Ardalis…).

**Pourquoi on ne l'adopte pas maintenant** :
1. Nos requêtes **write-side** sont **triviales** (`GetByIdAsync` charge l'agrégat + ses enfants) → Specification n'a presque rien à encapsuler.
2. Nos requêtes **flexibles/complexes** sont **read-side en Dapper** → **Specification ne s'applique pas à Dapper** (c'est EF/`IQueryable`).
3. Son usage courant pousse vers un **repository générique** — à rebours de nos repos **explicites par agrégat** choisis exprès.

**Quand le reconsidérer** : si les requêtes write-side d'agrégats se **diversifient** (combinaisons d'`Include`, filtres réutilisables, explosion de méthodes de repo type `GetByIdWithX`). C'est alors un **add-on propre** : `Ardalis.Specification` + `Ardalis.Specification.EntityFrameworkCore`, des classes `Specification` dans Application, et les repos qui les évaluent. Aucune réécriture du reste.

## Conséquences

- Domaine pur et testable ; frontière d'agrégat respectée ; lectures performantes via Dapper.
- Un peu plus de repos explicites à écrire (un par agrégat) — assumé.
- Si on ajoute Specification plus tard, on garde Dapper côté read (les deux coexistent).

## Alternatives écartées

- **`IAppDbContext` exposant les `DbSet`** (style « Jason Taylor ») : plus simple mais affaiblit l'encapsulation d'agrégat (un handler peut requêter/naviguer/projeter n'importe quoi). Écarté avec le passage au DDD riche.
- **Repository générique `IRepository<T>`** : redondant avec EF (le `DbContext` est déjà UoW+repo). Écarté.
- **Ardalis.Specification maintenant** : bénéfice faible vu nos requêtes write triviales + reads en Dapper. Reporté.
