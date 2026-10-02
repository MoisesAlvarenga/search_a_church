# Estado do Projeto

## Decisões

| ID | Decisão | Status | Origem |
| -- | ------- | ------ | ------ |
| AD-001 | A baseline do produto está organizada em cinco funcionalidades especificáveis de forma independente: busca, perfis, avaliações, mapas e autenticação. | Ativa | Migrado de `Old/docs/specs/` e `Old/docs/decisions/` em 2026-09-22 |
| AD-002 | A implementação legada é evidência do comportamento atual, mas não é a fonte de verdade para requisitos futuros. | Ativa | Regra de migração |
| AD-003 | Ambiguidades de produto e técnicas descobertas na migração permanecem como hipóteses explícitas até confirmação pelo responsável do produto. | Ativa | Critério de encerramento de Specify |

## Continuidade

- **Fase atual:** Specify
- **Escopo concluído:** Baseline inicial do produto extraída da pasta legada `Old`.
- **Funcionalidades:** `search-discovery`, `profile-management`, `reviews-feedback`, `maps-integration` e `authentication-authorization`.
- **Próxima ação:** Revisar e confirmar as hipóteses de cada `spec.md`. Não iniciar Design, Tasks ou Execute até que a especificação aplicável seja confirmada.
- **Deliberadamente não criados:** `context.md`, `design.md`, `tasks.md`, `validation.md`, código de implementação, testes ou documentos de arquitetura técnica.