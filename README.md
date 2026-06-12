# EAFCTracker — Backend

API REST em .NET 7 + PostgreSQL que coleta e expõe estatísticas de partidas do EA FC (clube, jogadores, playoffs, overall).

---

## Índice

- [Endpoints](#endpoints)
  - [Clubs](#clubs)
  - [Matches](#matches)
  - [Players](#players)
  - [Calendar](#calendar)
  - [Trends](#trends)
  - [Fetch](#fetch)
  - [Maintenance](#maintenance)
- [Modelo de dados](#modelo-de-dados)
  - [Diagrama de entidades](#diagrama-de-entidades)
  - [Entidades](#entidades)
- [Notas gerais](#notas-gerais)

---

## Endpoints

### Clubs

| Método | Rota | Query params | Retorno |
|--------|------|-------------|---------|
| GET | `/api/clubs` | — | `ClubListItemDto[]` |
| GET | `/api/clubs/{clubId}/overall` | `page` (def. 1) · `pageSize` (def. 20, máx. 200) | `PagedResult<ClubOverallStatsDto>` |
| GET | `/api/clubs/{clubId}/matches/overall` | `page` (def. 1) · `pageSize` (def. 10, máx. 100) | `PagedResult<MatchWithOverallStatsDto>` |
| GET | `/api/clubs/{clubId}/playoffs` | — | `ClubPlayoffAchievementDto[]` |
| GET | `/api/clubs/{clubId}/players/attributes` | `count` (def. 10) | `PlayerAttributeSnapshotDto[]` |
| GET | `/api/clubs/{clubId}/players/aggregate` | `count` (def. 10) · `opponentCount` (2–11) | `PlayerStatisticsDto[]` |
| GET | `/api/clubs/{clubId}/matches/statistics` | — | `FullMatchStatisticsDto` |
| GET | `/api/clubs/{clubId}/matches/statistics/limited` | `count` (def. 10) · `opponentCount`/`opp` (2–11) | `FullMatchStatisticsDto` |
| GET | `/api/clubs/{clubId}/matches/results` | `matchType` · `opponentCount`/`opp` · `page` · `pageSize` (máx. 200) | `PagedResult<MatchResultDto>` |
| GET | `/api/clubs/{clubId}/goals/analysis` | `from` (DateTime) · `to` (DateTime) | `GoalAnalysisDto` |
| DELETE | `/api/clubs/{clubId}/matches` | — | 204 |
| GET | `/api/clubs/matches/results` | `clubIds` (long[]) · `matchType` · `opponentCount`/`opp` · `page` · `pageSize` | `PagedResult<MatchResultDto>` |
| GET | `/api/clubs/matches/statistics/by-date-range-grouped` | `clubIds` (csv) · `start` · `end` · `opponentCount` | `FullMatchStatisticsByDayDto[]` |
| GET | `/api/clubs/matches/statistics/player/by-date-range-grouped` | `playerId` · `clubIds` (csv) · `start` · `end` | `PlayerStatisticsByDayDto[]` |
| GET | `/api/clubs/grouped/matches/statistics/limited` | `clubIds` (csv) · `count` (def. 20) · `opponentCount` | `object` |
| GET | `/api/clubs/records` | `clubIds` (csv) | `ClubRecordsDto` |
| GET | `/api/clubs/opponents` | `clubIds` (csv) | `OpponentsAnalysisDto` |

#### `GET /api/clubs/{clubId}/overall`
Histórico paginado de snapshots de overall stats do clube. Cada item representa o estado do clube no momento em que aquela partida foi processada.

- `matchId` é `null` em registros legados (capturados antes do histórico por partida ser implementado)
- Campos numéricos vêm como `string` (formato original da EA)
- Ordenado do mais recente para o mais antigo

#### `GET /api/clubs/{clubId}/matches/overall`
Últimas N partidas com o overall dos dois times (nosso clube + adversário) e o placar.

**Campo `result`:** `1` = vitória · `0` = derrota · `2` = empate

- Para partidas novas: `overallStats` é o snapshot exato daquele jogo
- Para partidas antigas: `overallStats` é o snapshot legado (estado mais recente conhecido)
- `overallStats` do adversário pode ser `null` se não foi coletado

**Exemplo de resposta:**
```json
{
  "page": 1, "pageSize": 10, "totalCount": 312, "totalPages": 32,
  "hasPrevious": false, "hasNext": true,
  "items": [
    {
      "matchId": 9283740192,
      "date": "2026-06-12T18:45:00Z",
      "ourClub": {
        "clubId": 355651, "clubName": "Los Galácticos",
        "goals": 3, "result": 1,
        "overallStats": { "skillRating": "1840", "wins": "202", "currentDivision": "1", "..." }
      },
      "opponent": {
        "clubId": 998877, "clubName": "FC Rival",
        "goals": 1, "result": 0,
        "overallStats": { "skillRating": "1710", "wins": "150", "currentDivision": "2", "..." }
      }
    }
  ]
}
```

#### `matchType` (enum)
| Valor | Significado |
|-------|-------------|
| `0` / `All` | Todos os tipos |
| `1` / `League` | Liga |
| `2` / `Playoff` | Playoff |

---

### Matches

| Método | Rota | Query params | Retorno |
|--------|------|-------------|---------|
| GET | `/api/matches` | `page` (def. 1) · `pageSize` (def. 50) | `PagedResult<MatchDto>` |
| GET | `/api/matches/{matchId}` | — | `MatchDto` |
| GET | `/api/matches/{matchId}/statistics` | — | `MatchStatisticsResponseDto` |
| GET | `/api/matches/{matchId}/event-aggregates` | — | `MatchEventAggregatesResponseDto` |
| GET | `/api/matches/{matchId}/players/{playerId}/statistics` | — | `MatchPlayerStatsDto` |
| GET | `/api/matches/{matchId}/goals` | — | `MatchGoalsResponseDto` |
| POST | `/api/matches/{matchId}/goals` | body: `RegisterGoalsRequest` | `{ message }` |
| DELETE | `/api/matches/{matchId}` | — | 204 |

---

### Players

| Método | Rota | Retorno |
|--------|------|---------|
| GET | `/api/players/{playerId}` | `PlayerEntity` |
| GET | `/api/players/{playerEntityId}/profile` | `PlayerProfileDto` |

---

### Calendar

| Método | Rota | Query params | Retorno |
|--------|------|-------------|---------|
| GET | `/api/calendar` | `year` · `month` · `clubId` ou `clubIds` (csv) | `CalendarMonthDto` |
| GET | `/api/calendar/day` | `date` (DateOnly) · `clubId` ou `clubIds` (csv) | `CalendarDayDetailsDto` |

---

### Trends

| Método | Rota | Query params | Retorno |
|--------|------|-------------|---------|
| GET | `/api/trends/club/{clubId}` | `last` (def. 30) · `since` · `until` (DateTime) | `object` |
| GET | `/api/trends/top-scorers` | `clubId` · `since` · `until` · `limit` (def. 10) | `object` |

---

### Fetch

| Método | Rota | Retorno |
|--------|------|---------|
| POST | `/api/fetch/run` | `{ ranAtUtc, hadErrors, errors[] }` |
| GET | `/api/fetch/last-run` | `{ lastFetchedAtUtc }` |

Dispara ou consulta o processo de coleta automática de partidas na EA FC API.

---

### Maintenance

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/maintenance/clubs/overall/refresh` | Atualiza overall de todos os clubes configurados |
| POST | `/api/maintenance/clubs/overall/refresh-all` | Idem (alias) |
| POST | `/api/maintenance/clubs/division/refresh-all` | Atualiza divisão de todos os clubes |
| POST | `/api/maintenance/clubs/members/enrich-all` | Enriquece dados de membros de todos os clubes |
| POST | `/api/maintenance/clubs/playoffs/refresh-all` | Atualiza playoffs de todos os clubes |
| POST | `/api/maintenance/clubs/refresh-everything` | Executa todos os refreshes acima em sequência |
| POST | `/api/maintenance/club/{clubId}/division/refresh` | `name` (opcional) — Atualiza divisão de um clube |
| POST | `/api/maintenance/club/{clubId}/members/enrich` | Enriquece membros de um clube |
| POST | `/api/maintenance/club/{clubId}/refresh-external` | `name` — Atualiza divisão + membros de um clube |
| POST | `/api/maintenance/club/{clubId}/opponents/division/refresh` | Atualiza divisão dos adversários recentes |
| GET | `/api/maintenance/matches/recent-aggregates` | `count` (def. 10, máx. 200) — Agrega eventos dos últimos N jogos |
| GET | `/api/maintenance/myip` | Retorna o IP público do servidor |

---

## Modelo de dados

### Diagrama de entidades

```
┌─────────────────┐
│   MatchEntity   │
│─────────────────│
│ PK MatchId      │◄──────────────────────────────────────┐
│    Timestamp    │                                        │
│    MatchType    │                                        │
└────────┬────────┘                                        │
         │ 1                                               │
         ├──────────────────── N ──────────────────────────┤
         │                                                 │
    ┌────┴──────────────────┐              ┌───────────────┴───────┐
    │    MatchClubEntity    │              │   MatchPlayerEntity   │
    │───────────────────────│              │───────────────────────│
    │ PK  Id                │              │ PK  MatchId (FK)      │
    │ FK  MatchId           │              │ PK  PlayerEntityId(FK)│
    │ UK  (MatchId, ClubId) │              │ PK  ClubId            │
    │     ClubId            │              │     Goals             │
    │     Goals             │              │     Assists           │
    │     GoalsAgainst      │              │     Rating            │
    │     Result (0/1/2)    │              │     Shots             │
    │     CurrentDivision   │              │     Redcards          │
    │     Date              │              │     Saves             │
    │     WinnerByDnf       │              │     Disconnected      │
    │                       │              │     ProOverall        │
    │ owned ──────────────► │              │     MatchEventAgg 0-3 │
    │  ClubDetailsEntity    │              │     [+ 30 campos]     │
    │  (Name, KitColors...) │              └──────────┬────────────┘
    └───────────────────────┘                         │ FK PlayerEntityId
                                                      │
                                          ┌───────────┴────────────┐
                                          │     PlayerEntity       │
                                          │────────────────────────│
                                          │ PK  Id                 │
                                          │ UK  (PlayerId, ClubId) │
                                          │     Playername         │
                                          │ FK  PlayerMatchStatsId │
                                          └───────────┬────────────┘
                                                      │ FK
                                                      ▼
                                          ┌───────────────────────┐
                                          │ PlayerMatchStatsEntity│
                                          │───────────────────────│
                                          │ PK  Id                │
                                          │ FK  PlayerEntityId    │
                                          │     Aceleracao        │
                                          │     Finalizacao       │
                                          │     PasseCurto        │
                                          │     Conducao          │
                                          │     [+ 30 atributos]  │
                                          └───────────────────────┘

┌───────────────────────┐
│  MatchGoalLinkEntity  │
│───────────────────────│
│ PK  Id                │
│ FK  MatchId ──────────────────────────────► MatchEntity
│     ClubId            │
│ FK  ScorerPlayerEntityId ─────────────────► PlayerEntity
│ FK? AssistPlayerEntityId ─────────────────► PlayerEntity (opcional)
│ FK? PreAssistPlayerEntityId ──────────────► PlayerEntity (opcional)
└───────────────────────┘

┌─────────────────────────┐      ┌────────────────────────────┐
│   OverallStatsEntity    │      │  PlayoffAchievementEntity  │
│─────────────────────────│      │────────────────────────────│
│ PK  Id                  │      │ PK  Id                     │
│     ClubId (indexed)    │      │ UK  (ClubId, SeasonId)     │
│     MatchId? (nullable) │      │     ClubId (indexed)       │
│     GamesPlayed         │      │     SeasonId               │
│     Wins / Losses / Ties│      │     SeasonName             │
│     Goals / GoalsAgainst│      │     BestDivision           │
│     SkillRating         │      │     BestFinishGroup        │
│     CurrentDivision     │      │     RetrievedAtUtc         │
│     Wstreak             │      └────────────────────────────┘
│     Unbeatenstreak      │
│     Reputationtier      │      ┌────────────────────────────┐
│     UpdatedAtUtc        │      │     SystemFetchAudit       │
└─────────────────────────┘      │────────────────────────────│
                                 │ PK  Id (singleton = 1)     │
 MatchId null  → registro legado │     LastFetchedAt          │
 MatchId != null → snapshot exato└────────────────────────────┘
```

---

### Entidades

#### MatchEntity
Representa uma partida. Ponto central do modelo.

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `MatchId` | `long` PK | ID da partida (vem da EA) |
| `Timestamp` | `DateTime` | Data/hora da partida |
| `MatchType` | `enum` | `League = 1` · `Playoff = 2` |

---

#### MatchClubEntity
Participação de um time em uma partida. Sempre existem 2 registros por partida.

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `Id` | `long` PK | Auto-gerado |
| `MatchId` | `long` FK | → MatchEntity |
| `ClubId` | `long` | ID do clube |
| `Goals` | `short` | Gols marcados |
| `GoalsAgainst` | `short` | Gols sofridos |
| `Result` | `short` | `1` vitória · `0` derrota · `2` empate |
| `CurrentDivision` | `int?` | Divisão no momento da partida |
| `Date` | `DateTime` | Data do jogo |
| `WinnerByDnf` | `bool` | Vitória por abandono do adversário |
| `Details` | owned | Nome, cores de kit, crest (embutido na tabela) |

> Unique index em `(MatchId, ClubId)`.

---

#### MatchPlayerEntity
Estatísticas de um jogador em uma partida específica. PK composta por `(MatchId, ClubId, PlayerEntityId)`.

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `Goals` / `Assists` / `Shots` | `short` | Stats ofensivas |
| `Rating` | `double` | Nota da partida (0–10) |
| `Redcards` | `short` | Cartões vermelhos |
| `Saves` | `short` | Defesas (goleiro) |
| `Disconnected` | `bool` | Desconectado durante o jogo |
| `ProOverall` | `int?` | Overall do Pro no momento |
| `ProHeight` | `int?` | Altura em cm |
| `MatchEventAggregate0–3` | `string` | Eventos do motor no formato `id:valor,id:valor,...` |
| `PlayerMatchStatsEntityId` | `long` FK | Atributos do Pro (compartilhado se não mudou) |

---

#### PlayerEntity
Jogador cadastrado. Identificado unicamente por `(PlayerId, ClubId)`.

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `Id` | `long` PK | Internal ID auto-gerado |
| `PlayerId` | `long` | ID original da EA |
| `ClubId` | `long` | Clube ao qual pertence |
| `Playername` | `string` | Nome do jogador |
| `PlayerMatchStatsId` | `long?` FK | Último snapshot de atributos |

---

#### PlayerMatchStatsEntity
Snapshot dos atributos do Pro (`vproattr`). Um novo registro é criado apenas quando os atributos mudam — vários `MatchPlayerEntity` podem apontar para o mesmo snapshot.

Contém os 35 atributos: `Aceleracao`, `Pique`, `Finalizacao`, `Cabeceio`, `ForcaDoChute`, `ChuteLonge`, `Voleio`, `Penalti`, `Visao`, `Cruzamento`, `Lancamento`, `PasseCurto`, `Curva`, `Agilidade`, `Equilibrio`, `ControleBola`, `Conducao`, `Interceptacaos`, `NocaoDefensiva`, `DivididaEmPe`, `Carrinho`, `Impulsao`, `Folego`, `Forca`, `Reacao`, `Combatividade`, `Frieza`, `ElasticidadeGL`, `ManejoGL`, `ChuteGL`, `ReflexosGL`, `PosGL`, `PosAtaqueInutil`.

---

#### OverallStatsEntity
Snapshot de estatísticas acumuladas de um clube. Gerado a cada partida processada (modelo 1:N).

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `Id` | `long` PK | Auto-gerado |
| `ClubId` | `long` | Clube (indexed) |
| `MatchId` | `long?` | Partida vinculada (`null` = registro legado) |
| `GamesPlayed` | `string?` | Total de partidas |
| `Wins` / `Losses` / `Ties` | `string?` | Resultados acumulados |
| `Goals` / `GoalsAgainst` | `string?` | Gols acumulados |
| `SkillRating` | `string?` | Classificação de habilidade |
| `CurrentDivision` | `int?` | Divisão atual |
| `Wstreak` / `Unbeatenstreak` | `string?` | Sequências |
| `Reputationtier` | `string?` | Nível de reputação |
| `LeagueAppearances` | `string?` | Aparições na liga |
| `UpdatedAtUtc` | `DateTime` | Momento da captura |

> **Todos os campos numéricos são `string`** — formato original da EA. Converter com `parseInt` / `parseFloat` antes de usar em cálculos.
>
> `MatchId = null` → registro legado (capturado antes do histórico por partida ser implementado). Serve como fallback nos endpoints que buscam overall por jogo.

---

#### PlayoffAchievementEntity
Conquistas de playoff de um clube por temporada. Um registro por `(ClubId, SeasonId)`.

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `ClubId` | `long` | Clube |
| `SeasonId` | `string` | ID da temporada (unique com ClubId) |
| `SeasonName` | `string?` | Nome legível da temporada |
| `BestDivision` | `string?` | Melhor divisão atingida |
| `BestFinishGroup` | `string?` | Melhor grupo de finalização |

---

#### MatchGoalLinkEntity
Vincula um gol a jogadores (artilheiro, assistente, pré-assistente) via análise de eventos.

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `MatchId` | `long` FK | Partida |
| `ClubId` | `long` | Clube que marcou |
| `ScorerPlayerEntityId` | `long` FK | Quem marcou |
| `AssistPlayerEntityId` | `long?` FK | Assistência (opcional) |
| `PreAssistPlayerEntityId` | `long?` FK | Pré-assistência (opcional) |

---

#### SystemFetchAudit
Singleton (`Id = 1` sempre) que registra o horário da última coleta automática.

| Campo | Tipo |
|-------|------|
| `LastFetchedAt` | `DateTimeOffset` |

---

## Notas gerais

- **Banco**: PostgreSQL via Npgsql + EF Core 7
- **Paginação**: todos os endpoints paginados retornam `PagedResult<T>` com `page`, `pageSize`, `totalCount`, `totalPages`, `hasPrevious`, `hasNext`, `items`
- **`opponentCount` / `opp`**: filtra partidas pelo número de jogadores únicos do adversário (2–11). Útil para excluir partidas contra bots
- **`MatchEventAggregate0–3`**: eventos do motor do jogo em formato `id:valor,id:valor,...`. O mapeamento de IDs para labels está em `MatchEventDefinitions` (com nível de confiança `confirmed` / `probable` / `ambiguous`)
- **Datas**: todas em UTC (`timestamp with time zone` no Postgres)
- **`opponentCount` aplicado a múltiplos clubes**: o filtro só é aplicado quando `clubIds` contém exatamente 1 clube
