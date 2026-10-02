# Especificação: Integração com Google Maps

## Objetivo
Garantir que a descoberta de igrejas seja totalmente integrada com Google Maps, incluindo exibição de marcadores, busca por lugares e recuperação de informações para igrejas não cadastradas.

## Contexto
O frontend deve usar Google Maps para renderizar o mapa, apresentar resultados georreferenciados e complementar as informações de igrejas usando a base de dados do Google Places.

## Critérios de aceitação
- [ ] O frontend usa Google Maps JavaScript API para renderizar o mapa.
- [ ] O sistema exibe marcadores para igrejas retornadas pela API.
- [ ] O usuário pode ver detalhes de cada igreja em um infowindow ou card sobre o mapa.
- [ ] O frontend consulta o Google Places para encontrar igrejas próximas mesmo que não estejam cadastradas no app.
- [ ] Igrejas encontradas via Google Places aparecem na lista com `source: maps`.
- [ ] A busca de perfil considera resultados do app e do Google Maps na mesma lista de ranking.
- [ ] O mapa pode ajustar `bounds` automaticamente para acomodar todos os resultados.

## Componentes e integração
### Mapa
- Componente principal de mapa usando Google Maps JS API
- Configuração do mapa, centro e zoom inicial com base na localização do usuário
- Marcadores customizados para mostrar diferentes tipos de igreja e origem

### Resultados do mapa
- Conectar cada marcador a um item de lista de ranking
- Ao clicar em um item da lista, centralizar o mapa e abrir o infowindow correspondente
- Ao clicar em um marcador, destacar o item correspondente na lista

### Busca de lugares
- Usar Google Places API ou Places Library para recuperar igrejas não cadastradas
- Parâmetros de busca:
  - localização central (`latitude`, `longitude`)
  - raio de busca
  - tipo ou palavra-chave relacionada a igrejas
- Mapear resultados do Places para o formato de `Church` parcial no app

### Geocodificação
- Usar Google Geocoding API quando necessário para converter endereço em coordenadas
- Permitir pesquisa por endereço ou por ponto atual do usuário

## Dados retornados do Google Maps
- `name`
- `address`
- `latitude`
- `longitude`
- `placeId`
- `rating` (quando disponível)
- `user_ratings_total` ou `reviewsCount` (quando disponível)
- `source: maps`

## Observações
- A implementação deve considerar limites de quota do Google Maps/APIs.
- Chaves devem ser armazenadas com segurança e injetadas no frontend via variáveis de ambiente ou configuração segura.
