# Estado do Projeto

## Decisões

| ID | Decisão | Status | Origem |
| -- | ------- | ------ | ------ |
| AD-001 | A baseline do produto está organizada em seis funcionalidades especificáveis de forma independente: busca e descoberta, gestão de perfis, avaliações, integração com mapas, autenticação/autorização e reivindicação de perfil (claim). | Ativa | Atualizado em 2026-10-02 com adição de `church-profile-claim` |
| AD-002 | A implementação legada é evidência do comportamento atual, mas não é a fonte de verdade para requisitos futuros. | Ativa | Regra de migração |
| AD-003 | Ambiguidades de produto e técnicas descobertas na migração permanecem como hipóteses explícitas até confirmação pelo responsável do produto. | Ativa | Critério de encerramento de Specify |
| AD-004 | A identificação estável de igrejas externas no Google Maps utiliza o `place_id` como âncora determinística para descoberta, deduplicação no mapa e vinculação a futuros perfis oficiais. | Ativa | Definido em `maps-integration` e `profile-management` em 2026-10-02 |
| AD-005 | O processo de reivindicação de perfil (`church-profile-claim`) adota validação estratificada em Tiers (Tier 1 público/operacional vs Tier 2 pleno/legal), ToS com declaração sob o art. 299 CP, retenção de logs do Marco Civil (Lei 12.965/2014) e rito de Notice and Takedown com descredenciamento sumário. | Ativa | Definido em `church-profile-claim` em 2026-10-02 |
| AD-006 | A autenticação nas APIs protegidas será baseada em tokens JWT (JSON Web Token) sem estado. | Ativa | Confirmado em `authentication-authorization` em 2026-10-02 |
| AD-007 | O acesso à visualização e busca no mapa (`GET`) exige usuário cadastrado e autenticado via JWT, não sendo uma funcionalidade pública aberta a visitantes anônimos. | Ativa | Confirmado em `authentication-authorization` e `maps-integration` em 2026-10-02 |
| AD-008 | A criação inicial de perfil de usuário (cadastro/onboarding) é pública e não exige autenticação prévia; alterações posteriores exigem autenticação JWT com autorização estrita baseada em propriedade (ownership), permitindo que apenas o próprio titular modifique seu perfil. | Ativa | Confirmado em `authentication-authorization` em 2026-10-02 |
| AD-009 | A gestão e alteração de informações do perfil de uma igreja são restritas exclusivamente a usuários credenciados como representantes verificados da congregação (status `Verified` via `church-profile-claim`). | Ativa | Confirmado em `authentication-authorization` em 2026-10-02 |
| AD-010 | A camada de sessão adota Pares de Tokens com Rotação Automática (RTR) e Expiração Deslizante (Access Token JWT de 15 min + Refresh Token de 60 dias), Detecção Automática de Reúso (revogação de `family_id`), armazenamento seguro em hardware móvel (iOS Keychain / Android Keystore) e Request Queuing no cliente HTTP. | Ativa | Confirmado em `authentication-authorization` em 2026-10-02 |
| AD-011 | A mitigação de abusos nos endpoints de autenticação (`/auth/login`, `/auth/register`, `/auth/refresh`) adota o algoritmo Sliding Window Counter com Redis, aplicando limites estritos por rota (`IP + email`, `IP` global e `user_id + device_id`) e retornando HTTP 429 com cabeçalhos padronizados IETF (`RateLimit` e `Retry-After`). | Ativa | Confirmado em `authentication-authorization` em 2026-10-02 |

## Continuidade

- **Fase atual:** Specify
- **Escopo concluído:** Baseline inicial do produto extraída da pasta legada `Old` e enriquecida com a especificação de vinculação pelo mapa, reivindicação de perfil (`church-profile-claim`), camada de sessão contínua resiliente e rate limiting com Redis.
- **Funcionalidades:** `search-discovery`, `profile-management`, `reviews-feedback`, `maps-integration`, `authentication-authorization` e `church-profile-claim`.
- **Rastreabilidade total:** 46 requisitos funcionais mapeados (`SEARCH-01..08`, `PROFILE-01..05`, `REV-01..05`, `MAP-01..07`, `AUTH-01..09`, `CLAIM-01..12`), todos com critérios em BDD/WHEN-THEN e testes independentes.
- **Próxima ação:** Revisar e confirmar as hipóteses de cada `spec.md` com o responsável pelo produto. Não iniciar Design, Tasks ou Execute até que a especificação aplicável seja confirmada.
- **Deliberadamente não criados:** `context.md`, `design.md`, `tasks.md`, `validation.md`, código de implementação, testes ou documentos de arquitetura técnica.

## Handoff

- **Feature**: `.specs/features/` (Todas as 6 funcionalidades de baseline e governança)
- **Phase / Task**: Specify — Consolidação e revisão das 6 especificações concluída
- **Completed**: Especificações completas com rastreabilidade, casos de borda e critérios de aceite:
  - `search-discovery` (SEARCH-01..08)
  - `profile-management` (PROFILE-01..05)
  - `reviews-feedback` (REV-01..05)
  - `maps-integration` (MAP-01..07)
  - `authentication-authorization` (AUTH-01..09)
  - `church-profile-claim` (CLAIM-01..12)
- **In-progress**: Aguardando validação/confirmação de hipóteses com o responsável do produto
- **Next step**: Confirmar as hipóteses registradas e selecionar a primeira funcionalidade para transição à fase Design
- **Blockers**: none
- **Uncommitted files**: `.specs/`
- **Branch**: main