# Especificação: Feedback de igrejas

## Objetivo
Permitir que usuários avaliem igrejas com estrelas e deixem comentários, fornecendo informações adicionais para o match e a decisão de visita.

## Contexto
A discovery platform deve oferecer confiança ao usuário. Avaliações ajudam a entender melhor cada igreja e permitem diferenciar resultados de origem app ou mapa.

## Critérios de aceitação
- [ ] Usuários podem avaliar igrejas com uma nota de 1 a 5 estrelas
- [ ] Usuários podem deixar comentários textuais opcionais
- [ ] A média de avaliação e o número de avaliações são exibidos nos resultados
- [ ] Comentários podem ser filtrados ou destacados por relevância
- [ ] Feedback funciona para igrejas cadastradas e, quando disponível, também para igrejas listadas via dados do mapa

## Cenários
### Cenário 1: Avaliar uma igreja cadastrada
- Dado que o usuário está visualizando a página de detalhes de uma igreja cadastrada
- Quando ele envia uma avaliação de 5 estrelas com comentário
- Então a avaliação é gravada e a média de rating é atualizada
- E o comentário aparece na lista de avaliações da igreja

### Cenário 2: Exibir avaliações nos resultados de busca
- Dado que existem avaliações de igrejas cadastradas
- Quando o usuário realiza uma busca por perfil e distância
- Então cada resultado exibe a média de rating e a quantidade de comentários

### Cenário 3: Verificação de origem da igreja
- Dado que uma igreja aparece apenas via dados do mapa
- Quando o resultado é exibido ao usuário
- Então ele consegue ver que a origem é `maps`
- E recebe informações básicas, mesmo que não haja avaliações ou cadastro completo
