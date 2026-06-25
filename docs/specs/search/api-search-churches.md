# API: Buscar igrejas

## Objetivo
Definir o contrato da API de busca de igrejas por perfil, localização e distância.

## Rota
`GET /api/churches/search`

## Parâmetros
- `profile` (string, opcional): perfil de igreja desejado.
- `latitude` (number, obrigatório): latitude do ponto de busca.
- `longitude` (number, obrigatório): longitude do ponto de busca.
- `radiusKm` (number, opcional): raio de busca em quilômetros.
- `language` (string, opcional): idioma preferido.
- `serviceType` (string, opcional): tipo de culto ou serviço.
- `userProfileId` (string, opcional): identificador do perfil do usuário para melhorar o match.
- `includeReviews` (boolean, opcional): se deve incluir resumo de avaliações e comentários nos resultados.
- `sortBy` (string, opcional): critério de ordenação, por padrão `matchScore`.

## Resposta 200
```json
{
  "results": [
    {
      "id": "string",
      "name": "string",
      "address": "string",
      "latitude": 0.0,
      "longitude": 0.0,
      "distanceKm": 0.0,
      "schedule": "string",
      "profileTags": ["string"],
      "serviceType": "string",
      "matchScore": 0.0,
      "rating": 4.5,
      "reviewsCount": 12,
      "isRegistered": true,
      "source": "app" // ou "maps"
    }
  ]
}
```

## Resposta 400
- Parâmetros inválidos ou localização ausente.

## Resposta 500
- Erro interno ao processar a busca.
