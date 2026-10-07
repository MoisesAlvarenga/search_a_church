# Tarefas: Autenticação, Autorização e Sessão Contínua

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: [`.specs/features/authentication-authorization/design.md`](design.md)  
**Status**: Draft  
**Stack**: Backend C# (.NET 8/9 Minimal APIs) + Cliente Mobile Flutter (Dart)  
**Cobertura de Requisitos**: `AUTH-01` a `AUTH-10`

---

## Test Coverage Matrix

> Generated from project guidelines and spec — confirm before Execute. Guidelines found: none — strong defaults applied.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| :--- | :--- | :--- | :--- | :--- |
| Domain / Services (`TokenService`, `AuthService`, `PasswordHasher`) | unit | Todas as ramificações; mapeamento 1:1 com os ACs da spec; todos os casos de borda cobertos | `backend/tests/SearchAChurch.UnitTests/**/*.cs` | `dotnet test --filter "Category=Unit"` |
| Infrastructure / Data Access (`DbContext`, `RedisRateLimiter`) | integration | Conexão real/testcontainers com PostgreSQL e Redis; atomicidade de scripts Lua e transações ACID | `backend/tests/SearchAChurch.IntegrationTests/**/*.cs` | `dotnet test --filter "Category=Integration"` |
| API Endpoints / Middleware (`/auth/*`, `RateLimitFilter`) | e2e | Todas as rotas: caminho feliz + erros 400, 401, 403, 429 + headers IETF | `backend/tests/SearchAChurch.E2ETests/**/*.cs` | `dotnet test --filter "Category=E2E"` |
| Client Storage & Interceptor (`SecureStorage`, `AuthInterceptor`) | unit | Simulação de concorrência com fila pausada, interceptação de 401 e renovação atômica | `frontend/test/auth/**/*_test.dart` | `flutter test test/auth/` |
| Client Auth State (`AuthCubit` / UI) | unit | Transições de estado determinísticas (`Unauthenticated`, `Authenticating`, `Authenticated`) | `frontend/test/auth/**/*_test.dart` | `flutter test test/auth/` |

---

## Parallelism Assessment

> Generated from codebase & environment — confirm before Execute.

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| :--- | :---: | :--- | :--- |
| Backend Unit Tests | Yes | Isolamento em memória por classe/método, sem dependência externa | Testes puros em xUnit com mocks/fakes de repositório |
| Backend Integration Tests | No | Uso de contêiner compartilhado de Redis / PostgreSQL por suite | Scripts Lua e truncamento de banco exigem execução sequencial |
| Backend E2E Tests | No | WebApplicationFactory com banco de testes e Redis | Testes com chamadas HTTP sequenciais por família de tokens |
| Flutter Client Unit/Widget Tests | Yes | Isolamento total em sandbox de Dart/Flutter com mocks de Dio e Storage | `testWidgets` e `test` isolados em memória |

---

## Gate Check Commands

| Gate Level | When to Use | Command |
| :--- | :--- | :--- |
| Quick | Após tarefas com testes unitários | `dotnet test --filter "Category=Unit"` (Backend) ou `flutter test` (Flutter) |
| Full | Após tarefas com integração/e2e | `dotnet test` (Executa Unit + Integration + E2E) |
| Build | Conclusão de fases ou alterações estruturais | `dotnet build && dotnet test` e `flutter analyze && flutter test` |

---

## Execution Plan

### Phase 1: Fundação & Domínio (Sequencial)
Estruturação da solução .NET, entidades relacionais e criptografia básica.
```
T1 ──→ T2 ──→ T3
```

### Phase 2: Mecanismo de Autenticação & Sessão Backend (Paralelo [P] condicionado)
Serviços essenciais de token, limitação de taxa e regras de negócio.
```
T3 ──→ T4 (TokenService)
T3 ──→ T5 [P] (RateLimiter Lua)
T3 ──→ T6 [P] (AuditLogger Marco Civil)
T4, T5, T6 ──→ T7 (Registro & Senha) ──→ T8 (Login & Família) ──→ T9 (RTR & Breach) ──→ T10 (Logout & OTP)
```

### Phase 3: Exposição de Endpoints & Middleware (Sequencial)
Filtro de taxa, autorização JWT e rotas públicas/protegidas.
```
T10 ──→ T11 (RateLimitFilter) ──→ T12 (JwtAuth & Policies) ──→ T13 (Minimal API Endpoints)
```

### Phase 4: Módulo Mobile no Flutter (Paralelo [P] no início)
Persistência segura em hardware, interceptor de rede com enfileiramento e gerenciamento de estado.
```
T13 ──→ T14 [P] (SecureStorage)
T13 ──→ T15 [P] (QueuedInterceptor Dio)
T14, T15 ──→ T16 (AuthRepository & AuthCubit)
```

---

## Task Breakdown

### T1: Criar Estrutura de Solução e Projetos .NET
**What**: Inicializar a Solution .NET 8/9 com o projeto Web API (`SearchAChurch.Api`) e o projeto de testes (`SearchAChurch.Tests`).  
**Where**: `backend/src/SearchAChurch.Api/SearchAChurch.Api.csproj`, `backend/tests/SearchAChurch.Tests/SearchAChurch.Tests.csproj`  
**Depends on**: None  
**Reuses**: Estrutura legada de controllers/program migrada para Minimal API  
**Requirement**: Infraestrutura base  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Solução compila sem erros via `dotnet build`
- [x] Pacotes NuGet essenciais adicionados: `Microsoft.AspNetCore.Authentication.JwtBearer`, `StackExchange.Redis`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `FluentValidation`
- [x] Projeto de testes configurado com xUnit e FluentAssertions  
**Tests**: none  
**Gate**: Build (`dotnet build`)  
**Commit**: `chore(backend): initialize .NET solution and test projects`

---

### T2: Modelar Entidades do PostgreSQL e DbContext
**What**: Criar as entidades de domínio `User`, `RefreshToken`, `PasswordResetOtp` e `AuditLog` com mapeamento EF Core e suporte a Soft Delete da LGPD.  
**Where**: `backend/src/SearchAChurch.Api/Data/AppDbContext.cs`, `backend/src/SearchAChurch.Api/Data/Entities/*.cs`  
**Depends on**: T1  
**Reuses**: Entidade `Church` e conceitos de auditoria de AD-014 e AD-024  
**Requirement**: AUTH-02, AUTH-05, AUTH-08  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Entidades mapeadas com precisão de tipos, chaves estrangeiras, índices e unicidade (`email`, `token_hash`)
- [x] Configuração de Soft Delete (`deleted_at`) para `User`
- [x] Migração inicial do EF Core gerada para PostgreSQL  
**Tests**: integration  
**Gate**: Full (`dotnet test --filter "Category=Integration"`)  
**Commit**: `feat(auth): configure entity framework models and postgresql migration`

---

### T3: Implementar Hashing de Senhas com BCrypt
**What**: Implementar interface `IPasswordHasher` utilizando BCrypt com Work Factor calibrado em 12.  
**Where**: `backend/src/SearchAChurch.Api/Services/PasswordHasher.cs`  
**Depends on**: T1  
**Reuses**: `BCrypt.Net-Next`  
**Requirement**: AUTH-02, AD-024  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Hashing seguro de senhas com sal aleatório e verificação de hash
- [x] Validação de política mínima: ≥ 8 caracteres, ao menos 1 letra e 1 número
- [x] Testes unitários cobrindo senhas válidas, fracas e hashes incompatíveis  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(auth): implement bcrypt password hasher with complexity validation`

---

### T4: Implementar TokenService (JWT 15m e Refresh Token Criptográfico)
**What**: Implementar emissão de Access Token JWT (15 minutos, claims `sub`, `exp`, `jti`, `family_id`) e geração de Refresh Token opaco com hash SHA-256.  
**Where**: `backend/src/SearchAChurch.Api/Services/TokenService.cs`  
**Depends on**: T1  
**Reuses**: `System.IdentityModel.Tokens.Jwt`  
**Requirement**: AUTH-01, AUTH-05, AD-006, AD-010  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Emissão de JWT com assinatura criptográfica HMAC-SHA256 e expiração exata de 15 minutos
- [x] Emissão de Refresh Token opaco de alta entropia (32 bytes Base64URL) com retorno do raw token e do hash SHA-256 para persistência
- [x] Extração de ClaimsPrincipal a partir de token expirado
- [x] Testes unitários validando expiração, claims e geração de hash determinístico  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(auth): implement jwt and cryptographic refresh token service`

---

### T5: Implementar RateLimiterService com Redis e Script Lua [P]
**What**: Implementar o algoritmo Sliding Window Counter no Redis através de script Lua atômico para conter força bruta e rajadas de requisições.  
**Where**: `backend/src/SearchAChurch.Api/Services/RedisRateLimiter.cs`, `backend/src/SearchAChurch.Api/Services/Scripts/sliding_window.lua`  
**Depends on**: T1  
**Reuses**: `StackExchange.Redis`  
**Requirement**: AUTH-09, AD-011  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Script Lua executa remoção de scores antigos (`ZREMRANGEBYSCORE`), contagem (`ZCARD`), adição (`ZADD`) e expiração (`PEXPIRE`) de forma atômica
- [x] Retorno com contagem restante e cálculo exato de segundos para `Retry-After`
- [x] Fail-open defensivo com log crítico em caso de timeout/falha do Redis
- [x] Testes unitários e de integração validando bloqueio no limite e liberação após a janela  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(auth): implement atomic sliding window rate limiter with redis lua script`

---

### T6: Implementar AuditLoggerService para o Marco Civil (180 dias) [P]
**What**: Implementar serviço de auditoria em modo append-only registrando `client_ip`, `client_port`, `timestamp_utc`, `user_agent` e `metadata` com retenção mínima de 180 dias.  
**Where**: `backend/src/SearchAChurch.Api/Services/MarcoCivilAuditLogger.cs`  
**Depends on**: T2  
**Reuses**: Entidade `AuditLog`  
**Requirement**: AUTH-02, AD-014  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Gravação estritamente append-only (sem métodos de update ou delete)
- [x] Extração segura do IP real a partir de proxies configurados
- [x] Testes unitários validando a preservação exata dos dados de conexão de aplicação  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(auth): implement marco civil audit logging service`

---

### T7: Implementar Registro de Usuário com Auditoria (RegisterAsync)
**What**: Implementar caso de uso de cadastro público de novos usuários com validação de payload, hashing de senha e disparo de log de auditoria.  
**Where**: `backend/src/SearchAChurch.Api/Features/Auth/RegisterHandler.cs`  
**Depends on**: T2, T3, T4, T6  
**Reuses**: `PasswordHasher`, `AuditLogger`  
**Requirement**: AUTH-02, AD-008, AD-014, AD-024  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Cadastro com sucesso gerando usuário ativo e par inicial de tokens
- [x] Rejeição de e-mail duplicado com código de validação sem vazar dados
- [x] Registro de log de auditoria contendo dados de conexão conforme Marco Civil
- [x] Testes unitários cobrindo cadastro com sucesso e falhas de validação  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(auth): implement user registration handler with audit logging`

---

### T8: Implementar Login de Usuário (LoginAsync)
**What**: Implementar autenticação de credenciais via e-mail e senha, criação de nova cadeia `family_id` e emissão de tokens.  
**Where**: `backend/src/SearchAChurch.Api/Features/Auth/LoginHandler.cs`  
**Depends on**: T2, T3, T4, T6  
**Reuses**: `PasswordHasher`, `TokenService`  
**Requirement**: AUTH-02, AUTH-05, AD-010  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [x] Validação correta de senha via BCrypt
- [x] Criação de `RefreshToken` com status `Active` e nova `family_id`
- [x] Retorno de erro HTTP 401 com mensagem genérica em caso de falha de credenciais
- [x] Testes unitários cobrindo login bem-sucedido e credenciais inválidas  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(auth): implement user login handler with token family generation`

---

### T9: Implementar Rotação Contínua (RTR) e Breach Detection (RefreshTokenAsync)
**What**: Implementar renovação silenciosa com queima do Refresh Token (`Consumed`), emissão do novo par com Sliding Expiration de 60 dias e revogação imediata da família sob reuso.  
**Where**: `backend/src/SearchAChurch.Api/Features/Auth/RefreshTokenHandler.cs`  
**Depends on**: T2, T4, T6  
**Reuses**: `TokenService`, `AuditLogger`  
**Requirement**: AUTH-05, AUTH-06, AD-010  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] Transação ACID atômica: queima token antigo para `Consumed` e gera novo `Active` com mesma `family_id`
- [ ] Se o token apresentado já estiver `Consumed`, revoga instantaneamente todos os tokens daquela `family_id` com status `Revoked` e retorna `TOKEN_BREACH_DETECTED`
- [ ] Tolerância de 2 segundos para requisições em trânsito com mesmo IP e `device_id`
- [ ] Testes unitários e de integração comprovando a rotação e a detecção de violação  
**Tests**: integration  
**Gate**: Full (`dotnet test --filter "Category=Integration"`)  
**Commit**: `feat(auth): implement refresh token rotation and automatic breach detection`

---

### T10: Implementar Logout e Recuperação de Senha via OTP (6 Dígitos)
**What**: Implementar logout idempotente (revogação de Refresh Token) e recuperação de senha via OTP numérico por e-mail (TTL 15 min, max 3 tentativas, revogação de todas as sessões).  
**Where**: `backend/src/SearchAChurch.Api/Features/Auth/LogoutHandler.cs`, `backend/src/SearchAChurch.Api/Features/Auth/PasswordResetHandler.cs`  
**Depends on**: T2, T3, T4, T6  
**Reuses**: `TokenService`, `PasswordHasher`  
**Requirement**: AUTH-08, AUTH-10, AD-024  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] Logout marca Refresh Token como `Revoked` sem necessidade de denylist de Access Tokens
- [ ] Solicitação de OTP gera código aleatório de 6 dígitos criptografado com TTL de 15 minutos
- [ ] Redefinição com sucesso invalida o OTP e revoga imediatamente todas as famílias ativas de tokens do usuário
- [ ] Testes unitários e de integração cobrindo logout, OTP válido, expirado e esgotamento de tentativas  
**Tests**: integration  
**Gate**: Full (`dotnet test --filter "Category=Integration"`)  
**Commit**: `feat(auth): implement logout and password reset handlers with email otp`

---

### T11: Implementar RateLimitFilter para Minimal APIs
**What**: Implementar filtro de endpoint em C# acoplado ao `RedisRateLimiter` injetando limites granulares por rota e retornando HTTP 429 com cabeçalhos IETF.  
**Where**: `backend/src/SearchAChurch.Api/Filters/RateLimitFilter.cs`  
**Depends on**: T5  
**Reuses**: `RedisRateLimiter`  
**Requirement**: AUTH-09, AD-011  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] Filtro extrai identificador correto (`IP + email`, `IP` global ou `user_id + device_id`)
- [ ] Cabeçalhos HTTP adicionados: `RateLimit-Limit`, `RateLimit-Remaining`, `RateLimit-Reset` e `Retry-After`
- [ ] Retorno padronizado HTTP 429 quando limite for excedido
- [ ] Testes unitários do filtro simulando limites permitidos e violados  
**Tests**: unit  
**Gate**: Quick (`dotnet test --filter "Category=Unit"`)  
**Commit**: `feat(auth): implement minimal api endpoint filter for rate limiting with ietf headers`

---

### T12: Configurar Autenticação JwtBearer e Políticas de Autorização
**What**: Configurar o middleware `JwtBearer` no pipeline do ASP.NET Core e definir políticas de autorização para Ownership e Representante Verificado de Igreja.  
**Where**: `backend/src/SearchAChurch.Api/Extensions/AuthenticationExtensions.cs`, `backend/src/SearchAChurch.Api/Authorization/ChurchRepresentativeRequirement.cs`  
**Depends on**: T4  
**Reuses**: `Microsoft.AspNetCore.Authentication.JwtBearer`  
**Requirement**: AUTH-01, AUTH-03, AUTH-04, AD-006, AD-007, AD-009  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] Validação rigorosa de assinatura, emissor, audiência e tempo de vida do token
- [ ] Rejeição de requisições sem token nas rotas protegidas (incluindo busca do mapa) com HTTP 401
- [ ] Política de autorização que restringe edição de igrejas a usuários com claim `is_verified_representative == true`
- [ ] Testes de integração validando passagem com token válido e bloqueio em token inválido/ausente  
**Tests**: integration  
**Gate**: Full (`dotnet test --filter "Category=Integration"`)  
**Commit**: `feat(auth): configure jwt bearer authentication and authorization policies`

---

### T13: Mapear Endpoints Minimal API de Autenticação (/auth/*)
**What**: Registrar todas as rotas `/auth` (`/register`, `/login`, `/refresh`, `/logout`, `/forgot-password`, `/reset-password`, `/me`) conectando filtros, handlers e documentação Swagger/OpenAPI.  
**Where**: `backend/src/SearchAChurch.Api/Endpoints/AuthEndpoints.cs`, `backend/src/SearchAChurch.Api/Program.cs`  
**Depends on**: T7, T8, T9, T10, T11, T12  
**Reuses**: Todos os handlers e filtros construídos nas etapas anteriores  
**Requirement**: AUTH-01 a AUTH-10  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] Todos os 7 endpoints registrados com respostas OpenAPI padronizadas
- [ ] Rota `/me` exige autorização JWT
- [ ] Testes de ponta a ponta (E2E) cobrindo fluxo completo: Cadastro → Login → Refresh → Consulta `/me` → Logout  
**Tests**: e2e  
**Gate**: Full (`dotnet test --filter "Category=E2E"`)  
**Commit**: `feat(auth): map auth minimal api endpoints and wire up application pipeline`

---

### T14: Implementar SecureStorageService no Flutter [P]
**What**: Criar serviço de armazenamento seguro no Flutter utilizando `flutter_secure_storage` configurado para Keychain no iOS e Keystore no Android.  
**Where**: `frontend/lib/core/storage/secure_storage_service.dart`  
**Depends on**: T13  
**Reuses**: `flutter_secure_storage`  
**Requirement**: AUTH-07, AD-010  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] Métodos para salvar, recuperar e limpar Access Token, Refresh Token e Device ID
- [ ] Configuração de `IOSOptions` com acessibilidade segura e `AndroidOptions` com EncryptedSharedPreferences
- [ ] Tratamento defensivo de corrupção de hardware sem travar a aplicação
- [ ] Testes unitários com mock do storage  
**Tests**: unit  
**Gate**: Quick (`flutter test test/core/storage/`)  
**Commit**: `feat(flutter): implement secure storage service for ios keychain and android keystore`

---

### T15: Implementar Dio QueuedInterceptor para Request Queuing e Silent Refresh [P]
**What**: Criar o interceptor HTTP no Flutter que enfileira requisições concorrentes, pausa o tráfego sob HTTP 401, executa o refresh silencioso e repassa os novos tokens.  
**Where**: `frontend/lib/core/network/auth_interceptor.dart`  
**Depends on**: T13  
**Reuses**: `dio.QueuedInterceptor`  
**Requirement**: AUTH-05, AUTH-07, AD-010  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] Injeta `Authorization: Bearer <token>` em todas as requisições autenticadas
- [ ] Pausa requisições paralelas durante a execução de `POST /auth/refresh`
- [ ] Atualiza o token no hardware seguro e reexecuta as requisições pendentes na fila
- [ ] Notifica callback de sessão expirada e limpa o storage se o refresh falhar
- [ ] Testes unitários cobrindo fluxo de refresh concorrente simulado com mock  
**Tests**: unit  
**Gate**: Quick (`flutter test test/core/network/`)  
**Commit**: `feat(flutter): implement dio queued interceptor for atomic silent token refresh`

---

### T16: Implementar AuthRepository e AuthCubit no Flutter
**What**: Implementar camada de dados e gerenciamento de estado de autenticação no Flutter com Cubit/Bloc expondo estados limpos para a UI.  
**Where**: `frontend/lib/features/auth/data/auth_repository.dart`, `frontend/lib/features/auth/presentation/cubit/auth_cubit.dart`  
**Depends on**: T14, T15  
**Reuses**: `SecureStorageService`, `DioClient`  
**Requirement**: AUTH-01 a AUTH-10  
**Tools**:
- MCP: NONE
- Skill: NONE  
**Done when**:
- [ ] Repositório conecta chamadas a `/register`, `/login`, `/logout` e `/forgot-password`
- [ ] `AuthCubit` gerencia estados `AuthInitial`, `Unauthenticated`, `Authenticating`, `Authenticated`
- [ ] Testes unitários do Cubit cobrindo transições de login, falha e logout  
**Tests**: unit  
**Gate**: Quick (`flutter test test/features/auth/`)  
**Commit**: `feat(flutter): implement auth repository and cubit state management`

---

## Parallel Execution Map

```
Phase 1 (Sequencial):
  T1 (Setup Solution) ──→ T2 (Entities & DbContext) ──→ T3 (PasswordHasher)

Phase 2 (Mecanismos & Core):
  T3 completa, então:
    ├── T4 (TokenService)
    ├── T5 [P] (RedisRateLimiter Lua)
    └── T6 [P] (AuditLogger Marco Civil)
  T4, T5, T6 completas, então:
    T7 (Register) ──→ T8 (Login) ──→ T9 (RTR & Breach) ──→ T10 (Logout & OTP)

Phase 3 (Endpoints & Middleware):
  T10 completa, então:
    T11 (RateLimitFilter) ──→ T12 (JwtAuth & Policies) ──→ T13 (Minimal API Endpoints)

Phase 4 (Cliente Flutter Mobile):
  T13 completa, então:
    ├── T14 [P] (SecureStorage)
    └── T15 [P] (QueuedInterceptor Dio)
  T14, T15 completas, então:
    T16 (AuthRepository & AuthCubit)
```

---

## Pre-Approval Validation Checks

### Check 1: Task Granularity

| Task | Scope / Entregável | Status |
| :--- | :--- | :---: |
| T1: Estrutura de Solução .NET | Configuração de projetos e referências | ✅ Granular |
| T2: Entidades PostgreSQL & DbContext | 4 entidades e DbContext mapeado | ✅ Granular |
| T3: Hashing de Senhas BCrypt | 1 serviço de criptografia de senha | ✅ Granular |
| T4: TokenService (JWT & Refresh) | 1 serviço de geração/validação de tokens | ✅ Granular |
| T5: RateLimiterService com Redis Lua | 1 serviço de rate limiting com script Lua | ✅ Granular |
| T6: AuditLogger Marco Civil | 1 serviço de gravação de logs | ✅ Granular |
| T7: RegisterAsync Handler | 1 caso de uso de registro de usuário | ✅ Granular |
| T8: LoginAsync Handler | 1 caso de uso de login e criação de sessão | ✅ Granular |
| T9: RefreshTokenAsync Handler | 1 caso de uso de rotação e detecção de reuso | ✅ Granular |
| T10: Logout & Password Reset Handlers | 2 handlers correlacionados de encerramento e OTP | ✅ Granular |
| T11: RateLimitFilter Minimal API | 1 filtro de endpoint ASP.NET Core | ✅ Granular |
| T12: JwtBearer & Políticas de Autorização | Configuração de autenticação e handlers de autorização | ✅ Granular |
| T13: Endpoints Minimal API (/auth/*) | Mapeamento de rotas e pipeline | ✅ Granular |
| T14: SecureStorageService Flutter | 1 serviço de armazenamento seguro mobile | ✅ Granular |
| T15: QueuedInterceptor Dio Flutter | 1 interceptor de rede com enfileiramento | ✅ Granular |
| T16: AuthRepository & AuthCubit Flutter | 1 repositório e 1 cubit de gerenciamento de estado | ✅ Granular |

---

### Check 2: Diagram-Definition Cross-Check

| Task | Depends On (Task Body) | Diagram Shows | Status |
| :--- | :--- | :--- | :---: |
| T1 | None | Início da Phase 1 | ✅ Match |
| T2 | T1 | T1 ──→ T2 | ✅ Match |
| T3 | T1 | T2 ──→ T3 | ✅ Match |
| T4 | T1 | T3 ──→ T4 | ✅ Match |
| T5 [P] | T1 | T3 ──→ T5 [P] | ✅ Match |
| T6 [P] | T2 | T3 ──→ T6 [P] | ✅ Match |
| T7 | T2, T3, T4, T6 | T4, T5, T6 ──→ T7 | ✅ Match |
| T8 | T2, T3, T4, T6 | T7 ──→ T8 | ✅ Match |
| T9 | T2, T4, T6 | T8 ──→ T9 | ✅ Match |
| T10 | T2, T3, T4, T6 | T9 ──→ T10 | ✅ Match |
| T11 | T5 | T10 ──→ T11 | ✅ Match |
| T12 | T4 | T11 ──→ T12 | ✅ Match |
| T13 | T7, T8, T9, T10, T11, T12 | T12 ──→ T13 | ✅ Match |
| T14 [P] | T13 | T13 ──→ T14 [P] | ✅ Match |
| T15 [P] | T13 | T13 ──→ T15 [P] | ✅ Match |
| T16 | T14, T15 | T14, T15 ──→ T16 | ✅ Match |

---

### Check 3: Test Co-location Validation

| Task | Code Layer Created / Modified | Matrix Requires | Task Tests Field | Status |
| :--- | :--- | :--- | :--- | :---: |
| T1 | Projeto & Configuração | none | none | ✅ OK |
| T2 | Entity / Data Access (DbContext) | integration | integration | ✅ OK |
| T3 | Domain Service (PasswordHasher) | unit | unit | ✅ OK |
| T4 | Domain Service (TokenService) | unit | unit | ✅ OK |
| T5 | Infrastructure (RedisRateLimiter) | unit | unit | ✅ OK |
| T6 | Infrastructure (MarcoCivilLogger) | unit | unit | ✅ OK |
| T7 | Application Handler (Register) | unit | unit | ✅ OK |
| T8 | Application Handler (Login) | unit | unit | ✅ OK |
| T9 | Application Handler (RefreshToken) | integration | integration | ✅ OK |
| T10 | Application Handler (Logout/Reset) | integration | integration | ✅ OK |
| T11 | Middleware / Filter (RateLimit) | unit | unit | ✅ OK |
| T12 | Middleware / Security Policies | integration | integration | ✅ OK |
| T13 | API Endpoints (/auth/*) | e2e | e2e | ✅ OK |
| T14 | Mobile Hardware Storage Service | unit | unit | ✅ OK |
| T15 | Mobile Network Interceptor | unit | unit | ✅ OK |
| T16 | Mobile State Management (Cubit) | unit | unit | ✅ OK |
