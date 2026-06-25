# Backlog inicial do MVP

## Objetivo
Organizar as primeiras entregas do projeto `search a church` em prioridades claras e alinhadas ao SDD.

## Prioridade 1 - MVP essencial
1. Definir o fluxo de descoberta por perfil e ranking de match
   - `docs/specs/search/find-church-by-profile.md`
   - `docs/specs/search/match-ranking.md`
2. Criar o contrato da API de busca
   - `docs/specs/search/api-search-churches.md`
3. Modelar perfis de usuário e igreja
   - `docs/specs/search/registration-and-match.md`
4. Definir feedback básico
   - `docs/specs/search/feedback.md`
5. Validar o MVP central
   - `docs/specs/mvp.md`

## Prioridade 2 - Implementação inicial do backend
- Criar o repositório `backend` com `.NET Minimal API`
- Implementar endpoint `GET /api/churches/search`
- Implementar modelagem de perfil de usuário e igreja
- Calcular `matchScore` e retornar resultados ordenados
- Permitir origem `app` e `maps`
- Adicionar dados de avaliação e comentários no retorno

## Prioridade 3 - Implementação inicial do frontend
- Criar o repositório `frontend` com Angular
- Implementar UI de busca e filtros de perfil
- Exibir resultados em lista de ranking e no mapa
- Mostrar origem, pontuação de match e avaliação das igrejas
- Permitir usuário criar/editar perfil de busca

## Prioridade 4 - Feedback e iteração
- Implementar CRUD de feedback de estrelas e comentários
- Exibir média de avaliações e o número de reviews
- Permitir comentários em detalhes da igreja
- Validar usabilidade do ranking e do mapa

## Priorização por repositório
### Repositório geral
- Documentação de regras e decisões
- Especificações de MVP, search, match e feedback
- Templates e padrões SDD

### Repositório backend
- API de busca e matching
- Modelos de dados para perfil, igreja, review
- Integração com PostgreSQL e JWT quando necessário

### Repositório frontend
- Componentes de lista de ranking e mapa
- Integração com Google Maps para exibir igrejas e resultado de busca
- Formulários de perfil e busca 
- Visualização de avaliações e detalhes de igreja

## Observações
- Manter o desenvolvimento guiado por especificações.
- Priorizar entrega de valor visível no MVP.
- Separar claramente o que pertence ao repositório de regras e documentação do que pertence aos repositórios de código.
