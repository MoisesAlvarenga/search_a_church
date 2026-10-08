# Tasks: Integração com Mapas

**Feature**: Integração com Mapas  
**Spec**: [`.specs/features/maps-integration/spec.md`](spec.md)  
**Design**: [`.specs/features/maps-integration/design.md`](design.md)  
**Status**: Ready for Execution  
**Coverage Goal**: 100% dos 7 requisitos funcionais (MAP-01 a MAP-07) cobertos com testes automatizados.

---

## Task Summary Table

| ID | Title | Status | Depends On | Tests | Gate |
|---|---|:---:|---|---|---|
| **T1** | Modelar Entidade Church e Migração no PostgreSQL com Âncora `place_id` | Done | NONE | unit | Quick |
| **T2** | Implementar Serviço de Cache Distribuído de Locais no Redis (PlacesCacheService) [P] | Done | NONE | unit | Quick |
| **T3** | Implementar Gateway da Google Maps Platform com Resiliência (GooglePlacesGateway) | Done | T2 | unit | Quick |
| **T4** | Implementar Motor de Deduplicação App-First (DeduplicationEngine) [P] | Done | T1 | unit | Quick |
| **T5** | Implementar Orquestrador de Descoberta Híbrida e Degradação Graciosa (MapOrchestratorService) | Done | T1, T3, T4 | integration | Full |
| **T6** | Mapear Endpoints Minimal API de Mapa (/map/*) com Proteção JWT | Done | T5 | e2e | Full |
| **T7** | Implementar Modelos de Dados e GeolocationService no Flutter [P] | Done | T6 | unit | Quick |
| **T8** | Implementar MapRepository e MapCubit com Debounce de 500ms | Todo | T7 | unit | Quick |
| **T9** | Implementar Tela de Mapa com GoogleMap, Marcadores, Clusters e Sincronização Bidirecional | Todo | T8 | widget | Quick |

---

## Phase 1: Fundações de Dados e Infraestrutura de Caching

### T1: Modelar Entidade Church e Migração no PostgreSQL com Âncora place_id
**What**: Mapear a entidade `Church` no EF Core com coordenadas geográficas, âncora determinística `place_id` (índice único parcial) e campos de verificação de liderança.  
**Where**: `backend/src/SearchAChurch.Api/Data/Entities/Church.cs`, `backend/src/SearchAChurch.Api/Data/AppDbContext.cs`  
**Depends on**: NONE  
**Reuses**: `AppDbContext`  
**Requirement**: MAP-01, MAP-06, AD-004, AD-025  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Entidade `Church` mapeada com índice único parcial em `PlaceId` (`WHERE place_id IS NOT NULL`)
- [x] Índices de coordenadas geográficas em `(Latitude, Longitude)`
- [x] Configuração de relacionamentos e soft delete no DbContext
- [x] Testes unitários validando configuração da entidade e integridade relacional  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(maps): create church entity model and database indexes for place_id`

---

### T2: Implementar Serviço de Cache Distribuído de Locais no Redis (PlacesCacheService) [P]
**What**: Implementar serviço de cache com Redis para locais da Google Places API com TTL configurável de até 30 dias (`AD-025`) e chave estável de `place_id`.  
**Where**: `backend/src/SearchAChurch.Api/Features/Maps/Services/PlacesCacheService.cs`  
**Depends on**: NONE  
**Reuses**: `StackExchange.Redis`  
**Requirement**: MAP-07, AD-025  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Gravação e recuperação de detalhes de locais serializados em JSON com TTL de 30 dias
- [x] Caching de resolução textual de geocodificação por hash do endereço
- [x] Tratamento defensivo de fail-open sem lançar exceções se o Redis estiver indisponível
- [x] Testes unitários com mock do Redis comprovando expiração e resiliência  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(maps): implement redis places cache service with 30-day ttl`

---

## Phase 2: Integração Externa e Motor de Deduplicação

### T3: Implementar Gateway da Google Maps Platform com Resiliência (GooglePlacesGateway)
**What**: Implementar cliente HTTP para Google Places API (Nearby Search e Place Details) e Geocoding API com Circuit Breaker, timeout de 3s e mapeamento para DTOs internos.  
**Where**: `backend/src/SearchAChurch.Api/Features/Maps/Gateways/GooglePlacesGateway.cs`  
**Depends on**: T2  
**Reuses**: `IPlacesCacheService`, `HttpClientFactory`  
**Requirement**: MAP-02, MAP-04, MAP-07, AD-025  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Métodos para busca por raio (`SearchNearbyPlacesAsync`), detalhes (`GetPlaceDetailsAsync`) e geocodificação (`GeocodeAddressAsync`)
- [x] Integração com `IPlacesCacheService` para consultar cache antes de consumir cota externa
- [x] Tratamento defensivo de erros externos (HTTP 5xx, timeout, cota `OVER_QUERY_LIMIT`) retornando falha tratada
- [x] Testes unitários com `HttpMessageHandler` mock simulando respostas de sucesso, cota excedida e falhas  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(maps): implement google places gateway with circuit breaker and caching`

---

### T4: Implementar Motor de Deduplicação App-First (DeduplicationEngine) [P]
**What**: Implementar algoritmo de unificação e deduplicação que compara resultados da base local (`origem: app`) e da Google Places (`origem: maps`) com base no `place_id`, aplicando precedência App-First (`AD-023`).  
**Where**: `backend/src/SearchAChurch.Api/Features/Maps/Services/DeduplicationEngine.cs`  
**Depends on**: T1  
**Reuses**: `ChurchMapItemDto`  
**Requirement**: MAP-04, MAP-05, MAP-06, AD-023  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Se um `place_id` da Google já existe na base oficial (`app`), o registro externo é sumariamente descartado (App-First)
- [x] Templos não cadastrados recebem `Source = ChurchSource.Maps`, `IsRegistered = false` e `CanClaim = true`
- [x] Cálculo determinístico de distância física (fórmula de Haversine) até o centro da pesquisa
- [x] Testes unitários cobrindo cenários com e sem sobreposição de `place_id`  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(maps): implement app-first deduplication engine for map results`

---

### T5: Implementar Orquestrador de Descoberta Híbrida e Degradação Graciosa (MapOrchestratorService)
**What**: Coordenar consultas simultâneas na base relacional do PostgreSQL e no `GooglePlacesGateway`, acionando o motor de deduplicação e aplicando degradação graciosa caso a Google falhe.  
**Where**: `backend/src/SearchAChurch.Api/Features/Maps/Services/MapOrchestratorService.cs`  
**Depends on**: T1, T3, T4  
**Reuses**: `AppDbContext`, `IGooglePlacesGateway`, `IDeduplicationEngine`  
**Requirement**: MAP-01, MAP-06, MAP-07, AD-025  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Execução paralela da consulta local no banco e consulta externa na Google API
- [x] Em caso de falha externa, retorna resultados locais com `IsDegraded = true` e mensagem amigável sem lançar erro 500
- [x] Retorno de DTO estruturado `MapSearchResponse` com metadados de centro e raio aplicado
- [x] Testes unitários e de integração comprovando a orquestração e a resiliência a falhas da API externa  
**Tests**: integration  
**Gate**: Full (`dotnet test --filter "Category=Integration"`)  
**Commit**: `feat(maps): implement map orchestrator service with graceful degradation`

---

## Phase 3: Endpoints Minimal API e Autorização

### T6: Mapear Endpoints Minimal API de Mapa (/map/*) com Proteção JWT
**What**: Expor rotas `/map/search`, `/map/places/{placeId}` e `/map/geocode` protegidas por JWT Bearer (`AD-007`), documentadas com OpenAPI/Swagger.  
**Where**: `backend/src/SearchAChurch.Api/Endpoints/MapEndpoints.cs`, `backend/src/SearchAChurch.Api/Program.cs`  
**Depends on**: T5  
**Reuses**: `IMapOrchestratorService`, `IGooglePlacesGateway`  
**Requirement**: MAP-01 a MAP-07, AD-007  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Rotas `/map/search`, `/map/places/{placeId}` e `/map/geocode` mapeadas com `RequireAuthorization()`
- [x] Validação de parâmetros de query (lat/lng válidos, raio positivo)
- [x] Rejeição imediata de acessos anônimos com HTTP 401 Unauthorized
- [x] Testes de integração/E2E cobrindo autorização, busca híbrida e geocodificação  
**Tests**: e2e  
**Gate**: Full (`dotnet test --filter "Category=E2E"`)  
**Commit**: `feat(maps): map protected map minimal api endpoints with jwt authorization`

---

## Phase 4: Cliente Mobile Flutter

### T7: Implementar Modelos de Dados e GeolocationService no Flutter [P]
**What**: Criar modelos `ChurchMapItemModel`, `MapSearchResponseModel` e serviço `GeolocationService` com permissões de GPS e fallback amigável.  
**Where**: `frontend/lib/features/maps/data/models/church_map_item_model.dart`, `frontend/lib/features/maps/core/geolocation_service.dart`  
**Depends on**: T6  
**Reuses**: `geolocator`  
**Requirement**: MAP-01, MAP-02, AD-025  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Serialização JSON completa com distinção de `Source` (`app` vs `maps`)
- [x] `GeolocationService` com tratamento de GPS desativado e permissão negada
- [x] Mensagens amigáveis sem lançar exceções não tratadas na UI
- [x] Testes unitários dos modelos e do serviço de geolocalização com mocks  
**Tests**: unit  
**Gate**: Quick (`flutter test test/features/maps/`)  
**Commit**: `feat(flutter): implement map models and geolocation service with permission handling`

---

### T8: Implementar MapRepository e MapCubit com Debounce de 500ms
**What**: Criar repositório `MapRepository` para chamadas a `/map/search` e `MapCubit` com controle de estados (`MapInitial`, `MapLoading`, `MapLoaded`, `MapErrorGraceful`) e debounce de 500ms em eventos de câmera.  
**Where**: `frontend/lib/features/maps/data/map_repository.dart`, `frontend/lib/features/maps/presentation/cubit/map_cubit.dart`  
**Depends on**: T7  
**Reuses**: `DioClient`, `AuthInterceptor`  
**Requirement**: MAP-01, MAP-03, MAP-07, AD-025  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] Repositório conecta `Dio` autenticado com Bearer JWT injetado
- [ ] `MapCubit` implementa debounce de 500ms antes de disparar nova busca de mapa
- [ ] Gerenciamento de seleção ativa de igreja para sincronização bidirecional
- [ ] Tratamento do status degradado (`isDegraded: true`) na transição de estado
- [ ] Testes unitários do repositório e do cubit  
**Tests**: unit  
**Gate**: Quick (`flutter test test/features/maps/`)  
**Commit**: `feat(flutter): implement map repository and cubit state management with debounce`

---

### T9: Implementar Tela de Mapa com GoogleMap, Marcadores, Clusters e Sincronização Bidirecional
**What**: Construir componente visual de mapa (`GoogleMap`) com marcadores distintos para `app` e `maps`, agrupamento de marcadores (*clustering*), BottomSheet sincronizado e CTA de reivindicação (`church-profile-claim`).  
**Where**: `frontend/lib/features/maps/presentation/screens/map_screen.dart`, `frontend/lib/features/maps/presentation/widgets/church_map_bottom_sheet.dart`  
**Depends on**: T8  
**Reuses**: `google_maps_flutter`, `MapCubit`  
**Requirement**: MAP-01, MAP-03, MAP-04, MAP-05, AD-025  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] Marcadores visuais diferenciados: oficial para `origem: app` e indicativo para `origem: maps`
- [ ] Agrupamento dinâmico (*clustering*) para marcadores sobrepostos no mesmo zoom
- [ ] Seleção no mapa focaliza o card na lista/bottom sheet e vice-versa (sincronização bidirecional)
- [ ] Botão destacado "Reivindicar esta igreja" em templos externos repassando dados pré-preenchidos para claim
- [ ] Banner discreto de degradação graciosa quando a API do Google Maps estiver indisponível
- [ ] Testes de widget cobrindo renderização, BottomSheet e CTA  
**Tests**: widget  
**Gate**: Quick (`flutter test test/features/maps/presentation/`)  
**Commit**: `feat(flutter): implement interactive map screen with clustering and bidirectional sync`

---

## Parallel Execution Map

```
Phase 1:
  ├── T1 (Church Entity & PlaceId Index)
  └── T2 [P] (Redis PlacesCacheService 30d)

Phase 2:
  T1 e T2 completas, então:
    ├── T3 (GooglePlacesGateway com Circuit Breaker) [depende de T2]
    └── T4 [P] (DeduplicationEngine App-First) [depende de T1]
  T3 e T4 completas, então:
    └── T5 (MapOrchestratorService Híbrido & Fallback)

Phase 3:
  T5 completa, então:
    └── T6 (MapEndpoints Minimal API com JWT)

Phase 4 (Mobile Client):
  T6 completa, então:
    └── T7 [P] (Models & GeolocationService) ──→ T8 (MapRepository & MapCubit Debounce) ──→ T9 (MapScreen GoogleMap & Clustering)
```
