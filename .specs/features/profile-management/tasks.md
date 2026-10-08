# Tasks: Gestão de Perfis de Usuários e Igrejas (profile-management)

**Feature**: Gestão de Perfis (Profile Management)  
**Spec**: [`.specs/features/profile-management/spec.md`](spec.md)  
**Design**: [`.specs/features/profile-management/design.md`](design.md)  
**Status**: Ready for Execution  
**Coverage Goal**: 100% dos 6 requisitos funcionais (PROFILE-01 a PROFILE-06) cobertos com testes automatizados.

---

## Task Summary Table

| ID | Title | Status | Depends On | Tests | Gate |
|---|---|:---:|---|---|---|
| **T1** | Modelar Entidades de Perfil de Usuário, Igreja Enriquecida, Horários e Catálogo de Tags no EF Core | Done | NONE | unit | Quick |
| **T2** | Implementar Repositório e Serviço de Catálogo de Tags com Cache Redis (TagCatalogService) [P] | Done | T1 | unit | Quick |
| **T3** | Implementar UserProfileService com Preferências, Raio e Soft Delete LGPD | Done | T1, T2 | unit | Quick |
| **T4** | Implementar ChurchProfileService com Validação de PlaceId, Horários e Concorrência Otimista | Todo | T1, T2 | unit | Quick |
| **T5** | Implementar Handlers de Autorização (Ownership e VerifiedRepresentativePolicy) | Todo | T1 | unit | Quick |
| **T6** | Mapear Endpoints Minimal API de Perfil de Usuário, Igreja e Catálogo (/profile/* e /tags/catalog) | Todo | T3, T4, T5 | e2e | Full |
| **T7** | Implementar Modelos, ProfileRemoteDataSource e ProfileRepository no Flutter [P] | Todo | T6 | unit | Quick |
| **T8** | Implementar UserProfileCubit, ChurchProfileCubit e TagCatalogCubit com Bloc Test | Todo | T7 | unit | Quick |
| **T9** | Implementar Telas de Perfil de Usuário, Edição de Igreja e Seletor de Tags no Flutter | Todo | T8 | widget | Quick |

---

## Phase 1: Fundações de Dados, Entidades EF Core e Catálogo de Tags

### T1: Modelar Entidades de Perfil de Usuário, Igreja Enriquecida, Horários e Catálogo de Tags no EF Core
**What**: Mapear as entidades `UserProfile`, `TagCatalog`, `ChurchMeetingSchedule` e as tabelas associativas `UserProfileTag` e `ChurchTag` no EF Core com migração PostgreSQL, além de estender a entidade existente `Church` com campos eclesiásticos (`Denomination`, `WorshipStyle`, `Languages`, contatos, `IsActive` e `ConcurrencyStamp`).  
**Where**: `backend/src/SearchAChurch.Api/Data/Entities/`, `backend/src/SearchAChurch.Api/Data/SearchAChurchDbContext.cs`  
**Depends on**: NONE  
**Reuses**: `SearchAChurchDbContext`, `Church`, `User`  
**Requirement**: PROFILE-01, PROFILE-03, PROFILE-04, PROFILE-05, AD-004, AD-026  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Entidade `UserProfile` mapeada com chave estrangeira 1:1 única para `User`, campos de preferências e timestamps UTC
- [x] Entidade `TagCatalog` mapeada com índice único em `Code`, enum `TagCategory` e seed data de 15 tags oficiais (Acessibilidade, Infraestrutura, Ministérios)
- [x] Entidade `ChurchMeetingSchedule` mapeada para persistir horários e cultos da congregação
- [x] Tabelas associativas N:N `UserProfileTag` e `ChurchTag` configuradas com chaves compostas
- [x] Entidade `Church` estendida com atributos eclesiásticos, flag `IsActive` e campo `ConcurrencyStamp` para concorrência otimista
- [x] Migração do EF Core gerada e aplicada com sucesso
- [x] Testes unitários validando configuração do modelo e integridade relacional  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(profile): create database entities, relations and migrations for user profiles, church extensions and tag catalog`

---

### T2: Implementar Repositório e Serviço de Catálogo de Tags com Cache Redis (TagCatalogService) [P]
**What**: Implementar repositório e serviço para consulta e validação de tags oficiais do sistema bidirecional, com agrupamento em 3 categorias (`Accessibility`, `Infrastructure`, `Ministries`), cache em memória no Redis com TTL de 24 horas e método para validação estrita de códigos de tags informados.  
**Where**: `backend/src/SearchAChurch.Api/Features/Profile/Services/TagCatalogService.cs`  
**Depends on**: T1  
**Reuses**: `SearchAChurchDbContext`, `IDistributedCache`  
**Requirement**: PROFILE-05, AD-026  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Interface `ITagCatalogService` e implementação `TagCatalogService` criadas
- [x] Consulta de catálogo agrupado em categorias com cache Redis chave `tags:catalog:active` (TTL 24h) e fallback gracioso ao banco
- [x] Método `ValidateTagCodesAsync` que valida se todas as tags enviadas existem e estão ativas, retornando tags inválidas
- [x] Testes unitários com mocks de DbContext e IDistributedCache cobrindo leitura com cache miss, cache hit e validação de tags  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(profile): implement tag catalog service with redis caching and vocabulary validation`

---

## Phase 2: Serviços de Domínio e Regras de Negócio (Usuário, Igreja e LGPD)

### T3: Implementar UserProfileService com Preferências, Raio e Soft Delete LGPD
**What**: Implementar serviço de domínio para gerenciamento de preferências do usuário (denominação, liturgia, idiomas, raio de busca $1.0 \le \text{radiusKm} \le 100.0$ e tags de necessidades), assegurando persistência como baseline para a busca e rotina de soft delete com anonimização cadastral e revogação de sessões em conformidade com o art. 18 da LGPD.  
**Where**: `backend/src/SearchAChurch.Api/Features/Profile/Services/UserProfileService.cs`  
**Depends on**: T1, T2  
**Reuses**: `SearchAChurchDbContext`, `ITagCatalogService`  
**Requirement**: PROFILE-01, PROFILE-02, PROFILE-06, AD-020, AD-021, AD-026  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Interface `IUserProfileService` e implementação `UserProfileService` criadas
- [x] `GetUserProfileAsync` retornando preferências e tags ou indicativo de perfil não configurado
- [x] `UpsertUserProfileAsync` validando faixa do raio padrão (1 a 100 km) e associando tags oficiais via `TagCatalogService`
- [x] Regra de negócio: filtros aplicados na busca nunca alteram as preferências salvas no perfil
- [x] `DeleteUserAccountAsync` executando soft delete (LGPD): despersonaliza `Name` para "Usuário Anônimo", gera hash do email, remove tags pessoais, invalida tokens e carimba `DeletedAt`
- [x] Testes unitários cobrindo upsert de preferências, validação de limites de raio e anonimização LGPD  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(profile): implement user profile service with baseline preferences and lgpd soft delete`

---

### T4: Implementar ChurchProfileService com Validação de PlaceId, Horários e Concorrência Otimista
**What**: Implementar serviço de domínio para gestão eclesiástica de congregações, incluindo consulta de perfil completo, atualização de dados cadastrais, cultos, idiomas e tags ofertadas, proteção estrita de unicidade do `place_id`, controle de concorrência otimista via `ConcurrencyStamp` e ciclo de vida de inativação temporária (`IsActive`).  
**Where**: `backend/src/SearchAChurch.Api/Features/Profile/Services/ChurchProfileService.cs`  
**Depends on**: T1, T2  
**Reuses**: `SearchAChurchDbContext`, `ITagCatalogService`  
**Requirement**: PROFILE-03, PROFILE-04, PROFILE-05, PROFILE-06, AD-004, AD-009, AD-026  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] Interface `IChurchProfileService` e implementação `ChurchProfileService` criadas
- [ ] `GetChurchProfileAsync` retornando dados cadastrais, contatos, horários de cultos e tags ofertadas
- [ ] `UpdateChurchProfileAsync` sincronizando cultos e tags validadas contra o catálogo
- [ ] Verificação de colisão de `place_id`: tentativa de associar `place_id` já existente em outra igreja ativa lança exceção com código `PLACE_ID_JA_VINCULADO` (HTTP 409)
- [ ] Verificação de concorrência otimista com `ConcurrencyStamp` para prevenir sobrescrita simultânea defasada
- [ ] `SetChurchStatusAsync` permitindo inativação (`IsActive = false`) sem exclusão física do histórico da igreja
- [ ] Testes unitários cobrindo atualização com sucesso, colisão de place_id, falha de concorrência e inativação  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(profile): implement church profile service with place id uniqueness and optimistic concurrency`

---

## Phase 3: Políticas de Autorização e Endpoints Minimal API

### T5: Implementar Handlers de Autorização (Ownership e VerifiedRepresentativePolicy)
**What**: Implementar handlers de autorização no ASP.NET Core para impor regras estritas de acesso: (1) política de Ownership exigindo que o usuário só acione leitura/escrita sobre seu próprio perfil (`sub == userId`); (2) política de Representante Verificado exigindo que alterações em igrejas só ocorram se o usuário autenticado for o `VerifiedRepresentativeUserId` com status `Verified`.  
**Where**: `backend/src/SearchAChurch.Api/Features/Profile/Authorization/`  
**Depends on**: T1  
**Reuses**: `IAuthorizationHandler`, `SearchAChurchDbContext`  
**Requirement**: PROFILE-02, PROFILE-03, AD-008, AD-009, AD-026  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] `OwnershipAuthorizationHandler` implementado validando correspondência entre `sub` claim do JWT e recurso
- [ ] `VerifiedRepresentativeAuthorizationHandler` implementado conferindo se o usuário é o representante legal verificado da congregação
- [ ] Registro das políticas `RequireProfileOwnership` e `RequireVerifiedRepresentative` no container de DI
- [ ] Retorno semântico de HTTP 403 `ACESSO_NEGADO_PROPRIEDADE` ou `REPRESENTANTE_NAO_VERIFICADO` quando a política falhar
- [ ] Testes unitários validando cada handler para cenários autorizados e negados  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(profile): implement ownership and verified representative authorization policies`

---

### T6: Mapear Endpoints Minimal API de Perfil de Usuário, Igreja e Catálogo (/profile/* e /tags/catalog)
**What**: Expor endpoints Minimal API para gestão de perfis e catálogo de tags com autenticação Bearer JWT obrigatória (exceto leitura pública de congregações ativas e catálogo), aplicação de políticas de autorização granular, validação de payload e tratamento padronizado de erros (HTTP 200, 400, 403, 404, 409).  
**Where**: `backend/src/SearchAChurch.Api/Features/Profile/Endpoints/ProfileEndpoints.cs`  
**Depends on**: T3, T4, T5  
**Reuses**: `IUserProfileService`, `IChurchProfileService`, `ITagCatalogService`  
**Requirement**: PROFILE-01, PROFILE-02, PROFILE-03, PROFILE-04, PROFILE-05, PROFILE-06  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] Endpoints mapeados: `GET /profile/user`, `PUT /profile/user`, `DELETE /profile/user`, `GET /profile/church/{id}`, `PUT /profile/church/{id}`, `PATCH /profile/church/{id}/status`, `GET /tags/catalog`
- [ ] Bloqueio com HTTP 403 ao tentar editar perfil de outro usuário ou igreja sem credencial `Verified`
- [ ] Resposta HTTP 409 com código `PLACE_ID_JA_VINCULADO` em colisões de congregações
- [ ] Testes de ponta a ponta (E2E) com `WebApplicationFactory<Program>` testando autenticação, autorização, CRUD de perfis e soft delete  
**Tests**: e2e  
**Gate**: Full (`dotnet test`)  
**Commit**: `feat(profile): map minimal api endpoints for user profiles, church management and tag catalog`

---

## Phase 4: Camada Mobile Flutter (Modelos, Cubits e Telas)

### T7: Implementar Modelos, ProfileRemoteDataSource e ProfileRepository no Flutter [P]
**What**: Implementar camada de dados no Flutter com modelos imutáveis, serialização JSON, cliente HTTP Dio para comunicação com os endpoints `/profile/*` e `/tags/catalog`, tratamento tipado de falhas de autorização e colisão de `place_id`.  
**Where**: `frontend/lib/features/profile/data/`  
**Depends on**: T6  
**Reuses**: `DioClient`, `AuthInterceptor`  
**Requirement**: PROFILE-01, PROFILE-03, PROFILE-04, PROFILE-05  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] Modelos de dados criados: `UserProfileModel`, `ChurchProfileModel`, `MeetingScheduleModel`, `TagCatalogModel`, `TagItemModel`
- [ ] `ProfileRemoteDataSource` e `ProfileRepository` implementados com métodos para perfil de usuário, igreja e catálogo de tags
- [ ] Tratamento tipado de exceções (`ForbiddenFailure`, `ConflictFailure`, `InvalidTagFailure`, `ProfileNotFoundFailure`)
- [ ] Testes unitários com mock HTTP cobrindo serialização e mapeamento de falhas  
**Tests**: unit  
**Gate**: Quick (`flutter test test/features/profile/data/`)  
**Commit**: `feat(profile): implement flutter models, remote data source and profile repository`

---

### T8: Implementar UserProfileCubit, ChurchProfileCubit e TagCatalogCubit com Bloc Test
**What**: Implementar Cubits no Flutter para orquestração de estados de perfil do usuário (carregar preferências, salvar dados, soft delete LGPD), perfil da igreja (carregar dados e cultos, salvar alterações, inativação) e catálogo oficial de tags com cache em memória.  
**Where**: `frontend/lib/features/profile/presentation/cubit/`  
**Depends on**: T7  
**Reuses**: `ProfileRepository`  
**Requirement**: PROFILE-01, PROFILE-03, PROFILE-05, PROFILE-06  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] `UserProfileCubit` gerenciando estados `Initial`, `Loading`, `Loaded`, `Saving`, `SaveSuccess`, `Deleting`, `Deleted` e `Error`
- [ ] `ChurchProfileCubit` gerenciando estados `Initial`, `Loading`, `Loaded`, `Saving`, `SaveSuccess` e `Error` (com flag de conflito de place_id)
- [ ] `TagCatalogCubit` carregando e disponibilizando as tags oficiais agrupadas por categoria
- [ ] Testes unitários com `bloc_test` validando todas as transições de estado  
**Tests**: unit  
**Gate**: Quick (`flutter test test/features/profile/presentation/cubit/`)  
**Commit**: `feat(profile): implement profile and tag cubits with state management and bloc tests`

---

### T9: Implementar Telas de Perfil de Usuário, Edição de Igreja e Seletor de Tags no Flutter
**What**: Implementar interfaces visuais completas no Flutter: componente reutilizável `TagSelectionChipsWidget` com abas/seções por categoria, tela `UserProfileScreen` com slider de raio e diálogo de exclusão de conta sob a LGPD, e tela `ChurchProfileEditScreen` para gestão de cultos, dados cadastrais e tags da igreja por representantes verificados.  
**Where**: `frontend/lib/features/profile/presentation/`  
**Depends on**: T8  
**Reuses**: `CustomButton`, `AppTheme`  
**Requirement**: PROFILE-01, PROFILE-03, PROFILE-05, PROFILE-06  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] `TagSelectionChipsWidget` exibindo categorias (Acessibilidade, Infraestrutura, Ministérios) com ícones e seleção interativa
- [ ] `UserProfileScreen` permitindo editar denominação, liturgia, slider de raio (1-100 km), seleção de tags e diálogo de encerramento de conta
- [ ] `ChurchProfileEditScreen` permitindo editar denominação, cultos/horários, contatos, tags ofertadas e switch de igreja ativa
- [ ] Testes de widget cobrindo renderização, seleção de tags, validação de formulários e acionamento de diálogos  
**Tests**: widget  
**Gate**: Quick (`flutter test test/features/profile/presentation/screens/`)  
**Commit**: `feat(profile): implement flutter user profile, church edit screens and tag selection chips`
