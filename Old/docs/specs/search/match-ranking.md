# Especificação: Ranking de match

## Objetivo
Definir como as igrejas são ordenadas por compatibilidade com o perfil do usuário, exibindo os resultados mais relevantes primeiro em uma lista de ranking.

## Contexto
Em vez de filtrar apenas as igrejas que atendem a todos os critérios, o sistema deve ordenar os resultados por pontuação de match, mostrando a igreja mais compatível no topo e mantendo uma lista hierarquizada.

## Critérios de aceitação
- [ ] O sistema calcula uma pontuação de match para cada igreja com base em perfil, distância, idioma, tipo de culto e feedback.
- [ ] Os resultados são exibidos em uma lista ordenada do maior para o menor `matchScore`.
- [ ] A distância é usada como um fator de ordenação, mas não exclui resultados por si só.
- [ ] Igrejas do mapa e igrejas cadastradas podem aparecer na lista, ordenadas pelo mesmo critério de match.
- [ ] Cada item da lista mostra a pontuação de match e indica se a igreja é proveniente do app ou do mapa.

## Padrões de ordenação
- `matchScore` deve considerar:
  - compatibilidade de perfil entre usuário e igreja
  - distância relativa ao ponto de pesquisa
  - idioma e tipo de culto
  - avaliações e número de comentários
  - precisão dos dados de perfil cadastrados
- Resultados com `isRegistered == true` podem ter prioridade leve quando o perfil estiver completo.

## Cenários
### Cenário 1: Exibir ranking do melhor match
- Dado que o usuário busca por perfil e localização
- E existem igrejas com diferentes níveis de compatibilidade
- Quando o sistema gera os resultados
- Então as igrejas são ordenadas do maior para o menor `matchScore`
- E o usuário vê a lista de ranking junto com o mapa

### Cenário 2: Mostrar origem e classificação
- Dado que um resultado é proveniente do mapa e outro do app
- Quando a lista é exibida
- Então cada item indica `source: app` ou `source: maps`
- E a ordem permanece baseada em `matchScore`
