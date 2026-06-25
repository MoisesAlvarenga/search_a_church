# Plano de Implementação do MVP

## Objetivo
Transitar da fase de especificação para a fase de execução, criando as primeiras entregas concretas do backend e do frontend.

## Etapas iniciais

### 1. Backend .NET Minimal API

#### 1.1 Criar o projeto backend
- Inicializar o repositório do backend em `backend/`
- Criar um projeto .NET Minimal API
- Configurar o `.gitignore` e README básico

#### 1.2 Definir modelos de domínio
- `Church`
  - `Id`, `Name`, `Address`, `Latitude`, `Longitude`, `Schedule`, `ProfileTags`, `ServiceType`, `Source`, `IsRegistered`
- `UserProfile`
  - `Id`, `Name`, `PreferredLanguages`, `PreferredStyles`, `PreferredServiceTypes`, `MaxDistanceKm`
- `Review`
  - `Id`, `ChurchId`, `UserId`, `Stars`, `Comment`, `CreatedAt`
- `MatchResult`
  - `ChurchId`, `MatchScore`, `DistanceKm`, `Rating`, `ReviewsCount`, `Source`

#### 1.3 Implementar o endpoint de busca
- `GET /api/churches/search`
- Parâmetros:
  - `profile`, `latitude`, `longitude`, `radiusKm`, `language`, `serviceType`, `userProfileId`, `includeReviews`, `sortBy`
- Resposta:
  - lista de igrejas ordenada por `matchScore`
  - campos adicionais: `rating`, `reviewsCount`, `isRegistered`, `source`
- Lógica de ranking:
  - compatibilidade de perfil
  - distância relativa
  - idioma e tipo de culto
  - avaliação e reviews
  - origem dos dados (`app` ou `maps`)

#### 1.4 Criar endpoints de suporte
- `POST /api/profiles` e `GET /api/profiles/{id}`
- `POST /api/reviews` e `GET /api/churches/{id}/reviews`
- Endpoints para obter perfis de usuário e enviar feedback

#### 1.5 Adicionar testes iniciais
- testes unitários para cálculo de `matchScore`
- testes de integração simples para `GET /api/churches/search`

### 2. Frontend Angular

#### 2.1 Criar o projeto frontend
- Inicializar o repositório Angular em `frontend/`
- Criar a estrutura básica de componentes e serviços
- Configurar rotas e layout inicial

#### 2.2 Implementar UI de busca e ranking
- Página de busca com campos de localização e perfil
- Lista de resultados ordenada por ranking
- Componente de mapa mostrando marcadores de igrejas com integração Google Maps
- Exibir origem, pontuação de match e avaliação de cada resultado

#### 2.2.1 Google Maps
- Configurar Google Maps JavaScript API no frontend
- Usar dados de localização para criar marcadores e bounds
- Exibir informações de igreja em infowindows ou cards sobre o mapa
- Consultar Google Places para identificar igrejas não cadastradas e recuperar dados como nome, endereço e coordenadas
- Permitir geocodificação reversa se o usuário fornecer um local ou endereço
- Atualizar a lista de ranking com base nos resultados do mapa e na origem `maps`

#### 2.3 Implementar formulário de perfil
- Formulário para usuário definir seu perfil de busca
- Integração com o backend para carregar e salvar perfil

#### 2.4 Implementar feedback no frontend
- Interface para enviar avaliações com estrelas e comentários
- Exibir média de rating e número de reviews no item de resultado

### 3. Entrega inicial do MVP

#### 3.1 Primeiro milestone
- Busca de igrejas por perfil e localização
- Lista de ranking de resultados em ordem de compatibilidade
- Exibição no mapa
- Modelo básico de dados no backend

#### 3.2 Segundo milestone
- Cadastro e edição de perfis de usuário
- Feedback de estrelas e comentários
- Integração de resultados de `maps` com origem identificada

### 4. Como usar este plano
- Comece pelo item `1.1` e avance sequencialmente.
- Mantenha os arquivos de especificação como fonte de verdade.
- Ao implementar, valide cada requisito com testes ou cenários de aceitação.

## Resultado esperado
Uma primeira versão do MVP que entrega:
- descoberta de igrejas por perfil e localização
- ranking de match em lista e mapa
- suporte a perfis de usuários e igrejas
- feedback básico com estrelas e comentários
- distinção entre igrejas cadastradas e mapas
