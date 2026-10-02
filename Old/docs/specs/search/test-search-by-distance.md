# Teste: Busca por distância

## Objetivo
Validar que a busca retorna apenas igrejas dentro do raio definido pelo usuário.

## Cenário
- Dado que o usuário define localização atual e raio de 10 km
- E existem igrejas dentro e fora desse raio
- Quando ele realiza a busca
- Então o sistema retorna apenas igrejas localizadas dentro de 10 km
- E os resultados são exibidos no mapa com distância calculada

## Dados de teste
- Localização de busca: latitude/longitude da cidade ou ponto de referência
- Raio: 10 km
- Resultado esperado: apenas igrejas com `distanceKm <= 10`
