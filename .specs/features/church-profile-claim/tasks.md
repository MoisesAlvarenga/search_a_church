# Tasks: Reivindicação de Perfil de Igreja (Claim)

**Feature**: Reivindicação de Perfil de Igreja  
**Spec**: [`.specs/features/church-profile-claim/spec.md`](spec.md)  
**Design**: [`.specs/features/church-profile-claim/design.md`](design.md)  
**Status**: Ready for Execution  
**Coverage Goal**: 100% dos 12 requisitos funcionais (CLAIM-01 a CLAIM-12) cobertos com testes automatizados.

---

## Task Summary Table

| ID | Title | Status | Depends On | Tests | Gate |
|---|---|:---:|---|---|---|
| **T1** | Modelar Entidades de Reivindicação, Disputa e Auditoria no EF Core | Done | NONE | unit | Quick |
| **T2** | Implementar Repositório e Serviço de Auditoria Append-Only (AuditLogService) [P] | Done | NONE | unit | Quick |
| **T3** | Implementar GeofencingService com Haversine Server-Side e Anti-Mock | Done | NONE | unit | Quick |
| **T4** | Implementar Gateways de Verificação de Provas (Social, OTP, QSA e RCPJ) [P] | Todo | NONE | unit | Quick |
| **T5** | Implementar DisputeResolutionEngine (Resolução Automática N1 e In_Dispute) | Todo | T1 | unit | Quick |
| **T6** | Implementar ClaimOrchestratorService e ClaimTtlBackgroundService | Todo | T1, T2, T3, T4, T5 | integration | Full |
| **T7** | Mapear Endpoints Minimal API de Reivindicação e Disputa (/claim/*) com JWT | Todo | T6 | e2e | Full |
| **T8** | Implementar Modelos, ClaimDataSource e ClaimRepository no Flutter [P] | Todo | T7 | unit | Quick |
| **T9** | Implementar ClaimCubit e DisputeCubit com Gerenciamento de Estados | Todo | T8 | unit | Quick |
| **T10** | Implementar Telas de Reivindicação, Câmera Geofence e Contestação no Flutter | Todo | T9 | widget | Quick |

---

## Phase 1: Fundações de Dados e Trilha de Auditoria Imutável (Marco Civil)

### T1: Modelar Entidades de Reivindicação, Disputa e Auditoria no EF Core
**What**: Mapear as entidades `ChurchClaim`, `ClaimEvidence`, `DisputeCase` e `ClaimAuditLog` no EF Core com migração PostgreSQL, relacionamentos e enums de estado e hierarquia probatória.  
**Where**: `backend/src/SearchAChurch.Api/Data/Entities/`, `backend/src/SearchAChurch.Api/Data/AppDbContext.cs`  
**Depends on**: NONE  
**Reuses**: `AppDbContext`, `Church`  
**Requirement**: CLAIM-01, CLAIM-07, CLAIM-08, AD-005, AD-014  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Entidade `ChurchClaim` mapeada com status, target tier, método de validação, aceite de ToS, campos TTL e índices parciais
- [x] Entidades `ClaimEvidence`, `DisputeCase` e `ClaimAuditLog` mapeadas com tipos e relacionamentos
- [x] Atualização da entidade `Church` com campos `ClaimStatus`, `VerificationTier`, `VerifiedRepresentativeUserId` e `VerifiedAt`
- [x] Migração do EF Core gerada e aplicada com sucesso no PostgreSQL
- [x] Testes unitários validando configuração do modelo e integridade relacional  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(claim): create database entities and migrations for claims, disputes and audit logs`

---

### T2: Implementar Repositório e Serviço de Auditoria Append-Only (AuditLogService) [P]
**What**: Implementar repositório e serviço append-only inviolável para gravação de registros de conexão de aplicação (`client_ip`, `client_port`, `timestamp_utc`, `user_agent`, `verification_metadata`, retenção mínima de 180 dias) em estrita conformidade com o Marco Civil da Internet (art. 15).  
**Where**: `backend/src/SearchAChurch.Api/Features/Claim/Services/AuditLogService.cs`  
**Depends on**: NONE  
**Reuses**: `AppDbContext`, `IHttpContextAccessor`  
**Requirement**: CLAIM-06, CLAIM-07, AD-014  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Serviço `IAuditLogService` com método `RecordEventAsync` extraindo IP/Porta reais do contexto HTTP de forma sanitizada
- [x] Bloqueio lógico e garantia append-only (sem métodos de update ou delete permitidos na camada de aplicação)
- [x] Cálculo automático de retenção de 180 dias (`DateTime.UtcNow.AddDays(180)`)
- [x] Testes unitários cobrindo extração de IPv4/IPv6, user-agent e persistência de metadados  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(claim): implement append-only audit log service under marco civil compliance`

---

## Phase 2: Motores de Domínio, Algoritmos e Validações Core

### T3: Implementar GeofencingService com Haversine Server-Side e Anti-Mock
**What**: Implementar serviço de validação de presença física geodésica estritamente no backend usando fórmula de Haversine com tolerância máxima de 100m, precisão de GPS horizontal <= 50m e recusa sumária de mock locations.  
**Where**: `backend/src/SearchAChurch.Api/Features/Claim/Services/GeofencingService.cs`  
**Depends on**: NONE  
**Reuses**: Math helpers  
**Requirement**: CLAIM-02, AD-017  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Método `CalculateHaversineDistanceMeters` implementado com precisão geodésica com raio da Terra R = 6.371.000m
- [x] Validação rejeitando `isMockLocation == true` com código `LOCALIZACAO_SIMULADA_DETECTADA`
- [x] Validação rejeitando precisão > 50m com código `PRECISAO_GPS_INSUFICIENTE`
- [x] Validação rejeitando distância > 100m com código padronizado `FORA_DO_RAIO_PERMITIDO`
- [x] Aprovação com sucesso em raio <= 100m e precisão <= 50m
- [x] Testes unitários com casos de borda geográficos cobrindo todas as condições e códigos de erro  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(claim): implement server-side haversine geofencing service with anti-mock validation`

---

### T4: Implementar Gateways de Verificação de Provas (Social, OTP, QSA e RCPJ) [P]
**What**: Implementar gateways de geração e conferência de provas para Redes Sociais (token de bio com TTL 48h em Redis), E-mail com OTP institucional (validade 15 min), Validação QSA (cruzamento CPF/CNPJ) e Hash documental RCPJ.  
**Where**: `backend/src/SearchAChurch.Api/Features/Claim/Gateways/`  
**Depends on**: NONE  
**Reuses**: `StackExchange.Redis`, `HttpClientFactory`  
**Requirement**: CLAIM-03, CLAIM-04, CLAIM-05, AD-012, AD-015  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] Gateway `ISocialVerificationGateway` gerando e validando token `SAC-XXXX-VERIFY` com TTL de 48 horas em Redis
- [ ] Gateway `IDomainEmailGateway` gerando OTP numérico de 6 dígitos com TTL de 15 minutos e validação de domínio
- [ ] Gateway `IQsaValidationGateway` validando representação legal no QSA da Receita Federal
- [ ] Cálculo e armazenamento de hash SHA-256 para documentos cartorários em PDF
- [ ] Testes unitários para cada gateway com mocks de Redis e serviços externos  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(claim): implement verification gateways for social bio, email otp, qsa and rcpj`

---

### T5: Implementar DisputeResolutionEngine (Resolução Automática N1 e In_Dispute)
**What**: Implementar motor de governança de contestações executando a Regra de Resolução Automática de Nível 1 sobre Níveis 2/3, instauração de litígio paritário `In_Dispute` com congelamento de perfil/PIX por 5 dias úteis, e resolução por prevalência registral RCPJ ou inércia.  
**Where**: `backend/src/SearchAChurch.Api/Features/Claim/Services/DisputeResolutionEngine.cs`  
**Depends on**: T1  
**Reuses**: `AppDbContext`, `IAuditLogService`  
**Requirement**: CLAIM-08, CLAIM-10, CLAIM-11, CLAIM-12, AD-012, AD-016  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] Resolução automática revogando sumariamente vínculos de Nível 2 ou 3 perante contestação de Nível 1 válida
- [ ] Notificação ao titular anterior por prevalência documental legal sem conceder bloqueio unilateral
- [ ] Instauração de `In_Dispute` para disputas de mesma hierarquia (N1 vs N1) com bloqueio imediato de edições e PIX
- [ ] Cálculo da janela improrrogável de 5 dias úteis para juntada de certidão atualizada do RCPJ
- [ ] Resolução por averbação mais recente no RCPJ e desclassificação sumária por inércia processual
- [ ] Reversão do status para `Unclaimed` em caso de litígio juridicamente irresolvível
- [ ] Testes unitários exaustivos cobrindo todos os cenários de disputa e congelamento  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(claim): implement dispute resolution engine with tier 1 override and parity dispute handling`

---

### T6: Implementar ClaimOrchestratorService e ClaimTtlBackgroundService
**What**: Implementar serviço orquestrador de ciclo de vida de reivindicação coordenando aceite obrigatório de ToS (art. 299 CP e Provedora de Aplicação), transições de estado, rate limits (máximo 3 tentativas / lockout 72h) e worker background em segundo plano para timeouts (7d doc / 48h social) com disparo de lembrete preventivo 24h antes.  
**Where**: `backend/src/SearchAChurch.Api/Features/Claim/Services/ClaimOrchestratorService.cs`, `backend/src/SearchAChurch.Api/Features/Claim/Services/ClaimTtlBackgroundService.cs`  
**Depends on**: T1, T2, T3, T4, T5  
**Reuses**: `AppDbContext`, `IAuditLogService`, `IGeofencingService`, `IDisputeResolutionEngine`  
**Requirement**: CLAIM-01, CLAIM-06, CLAIM-08, CLAIM-09, AD-005, AD-013, AD-015  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] Orquestrador validando aceite de ToS e gravando evento de início de claim na trilha de auditoria
- [ ] Transição correta `Unclaimed` -> `Pending_Verification` com atribuição de TTL conforme o método selecionado
- [ ] Aprovação de evidência transicionando para `Verified` com concessão de tier e atualização da congregação
- [ ] Bloqueio de cadastros simultâneos na mesma congregação
- [ ] Worker em background (`IHostedService`) executando a cada 30 minutos, expirando claims vencidos e liberando congregações para `Unclaimed`
- [ ] Detecção de claims com 24h restantes e disparo de notificação preventiva de lembrete
- [ ] Testes de integração cobrindo o fluxo completo e o comportamento do worker em background  
**Tests**: integration  
**Gate**: Full (`dotnet test`)  
**Commit**: `feat(claim): implement claim orchestrator service and background ttl worker`

---

## Phase 3: Exposição de APIs e Autorização Granular

### T7: Mapear Endpoints Minimal API de Reivindicação e Disputa (/claim/*) com JWT
**What**: Expor endpoints Minimal API sob o grupo `/claim` com autenticação Bearer JWT obrigatória, rate limiting, extração de metadados de rede para auditoria do Marco Civil, submissão de evidências e consulta de status.  
**Where**: `backend/src/SearchAChurch.Api/Features/Claim/Endpoints/ClaimEndpoints.cs`  
**Depends on**: T6  
**Reuses**: `IClaimOrchestratorService`, `IDisputeResolutionEngine`  
**Requirement**: CLAIM-01, CLAIM-02, CLAIM-03, CLAIM-04, CLAIM-05, CLAIM-06, CLAIM-07, CLAIM-08, CLAIM-09, CLAIM-10, CLAIM-11, CLAIM-12  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] Endpoints implementados: `/claim/initiate`, `/claim/verify/geofence`, `/claim/verify/social-bio/generate`, `/claim/verify/social-bio/confirm`, `/claim/verify/domain/send-otp`, `/claim/verify/domain/confirm-otp`, `/claim/verify/document/rcpj`, `/claim/verify/document/qsa`, `/claim/dispute/contest`, `/claim/dispute/{id}/submit-certificate` e `/claim/status/{churchId}`
- [ ] Resposta com códigos HTTP semânticos (200 OK, 400 Bad Request, 409 Conflict)
- [ ] Extração de IP e porta do cliente injetados na trilha de auditoria
- [ ] Testes de ponta a ponta (E2E) com WebApplicationFactory testando cada rota sob autenticação  
**Tests**: e2e  
**Gate**: Full (`dotnet test`)  
**Commit**: `feat(claim): map minimal api endpoints for claim lifecycle, verification and disputes`

---

## Phase 4: Camada Mobile Flutter (UI, Sensores, Bloc/Cubit e Integração)

### T8: Implementar Modelos, ClaimDataSource e ClaimRepository no Flutter [P]
**What**: Implementar camada de dados no Flutter com modelos imutáveis, serialização JSON, cliente HTTP Dio para comunicação com os endpoints `/claim/*`, tratamento de erros tipados de validação e rastreamento de status.  
**Where**: `frontend/lib/features/claim/data/`  
**Depends on**: T7  
**Reuses**: `DioClient`  
**Requirement**: CLAIM-01, CLAIM-06, CLAIM-07  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] Modelos de dados criados: `ChurchClaimModel`, `VerificationResultModel`, `DisputeCaseModel`, `ClaimStatusModel`
- [ ] DataSource e Repository implementados cobrindo todas as chamadas de API de claim e disputa
- [ ] Tratamento tipado de exceções (`GeofenceException`, `MockLocationException`, `ConflictException`)
- [ ] Testes unitários com mock HTTP cobrindo parsing e cenários de erro  
**Tests**: unit  
**Gate**: Quick (`flutter test test/features/claim/data/`)  
**Commit**: `feat(claim): implement flutter models, remote data source and claim repository`

---

### T9: Implementar ClaimCubit e DisputeCubit com Gerenciamento de Estados
**What**: Implementar Cubits no Flutter para orquestração de estados de reivindicação e contestação, validação reativa de formulário de ToS, contagem regressiva de TTL de expiração e emissão de eventos amigáveis para a UI.  
**Where**: `frontend/lib/features/claim/presentation/cubit/`  
**Depends on**: T8  
**Reuses**: `ClaimRepository`  
**Requirement**: CLAIM-01, CLAIM-02, CLAIM-09, CLAIM-11  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] `ClaimCubit` gerenciando estados `Initial`, `Submitting`, `Initiated`, `Verifying`, `VerifiedSuccess` e `Error`
- [ ] `DisputeCubit` gerenciando abertura de contestações, upload de certidões e acompanhamento do prazo de 5 dias úteis
- [ ] Mensagens de erro padronizadas mapeadas para a interface do usuário
- [ ] Testes unitários com `bloc_test` validando todas as transições de estado  
**Tests**: unit  
**Gate**: Quick (`flutter test test/features/claim/presentation/cubit/`)  
**Commit**: `feat(claim): implement claim and dispute cubits with bloc test coverage`

---

### T10: Implementar Telas de Reivindicação, Câmera Geofence e Contestação no Flutter
**What**: Implementar interfaces visuais do fluxo de claim: tela inicial com ToS (art. 299 CP e Provedora de Aplicação), seleção de método pelos 3 níveis probatórios, tela de geofencing com checagem anti-mock e câmera ao vivo, formulário de disputa e vinculação com o botão "Reivindicar esta igreja" do mapa.  
**Where**: `frontend/lib/features/claim/presentation/screens/`  
**Depends on**: T9  
**Reuses**: `ChurchMapBottomSheet`, `CustomButton`, `AppTheme`  
**Requirement**: CLAIM-01, CLAIM-02, CLAIM-03, CLAIM-04, CLAIM-05, CLAIM-06, CLAIM-10, CLAIM-11  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] `ClaimInitiationScreen` recebendo argumentos da igreja pré-preenchidos do mapa, com checkboxes mandatórios de ToS
- [ ] `ClaimMethodSelectionScreen` exibindo os 3 níveis probatórios com explicação transparente dos poderes e selos
- [ ] `ClaimGeofenceCameraScreen` com leitura de GPS, verificação anti-mock, captura de foto ao vivo e feedback de distância
- [ ] `ClaimDisputeScreen` permitindo anexar certidão cartorial do RCPJ e exibindo cronômetro do prazo de 5 dias úteis
- [ ] Conexão da navegação da rota `/claim` ao acionar *"Reivindicar esta igreja"* em `ChurchMapBottomSheet`
- [ ] Testes de widget cobrindo renderização, interações de clique, validação de checkboxes e feedback de erro  
**Tests**: widget  
**Gate**: Quick (`flutter test test/features/claim/presentation/screens/`)  
**Commit**: `feat(claim): implement flutter claim initiation, method selection, geofence camera and dispute screens`
