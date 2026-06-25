# Spec Driven Development (SDD) para Search a Church

## Objetivo
Este documento descreve o passo a passo de desenvolvimento usando Spec Driven Development para o projeto `search a church`.

## Visão Geral
SDD significa criar o projeto a partir das especificações e critérios de aceitação. O foco é garantir que o produto final reflita o comportamento esperado antes de implementar o código.

## Etapas do projeto com SDD

### 1. Definir o escopo do MVP
- Identificar o valor principal do produto.
- Para o `search a church`, o MVP é:
  - descoberta de igrejas por perfil e distância
  - exibição em mapa
  - resultados relevantes para viajantes e pessoas em busca de igreja

### 2. Coletar requisitos e hipóteses
- Reunir necessidades de usuários e casos de uso.
- Exemplo:
  - "Usuário viajante quer encontrar igrejas próximas com estilo de culto compatível"
  - "Usuário quer ver a igreja no mapa e filtrar por distância"

### 3. Escrever especificações de alto nível
- Criar arquivos em `docs/specs/` usando o template `spec.template.md`.
- Incluir:
  - objetivo de negócio
  - contexto
  - critérios de aceitação
  - cenários `Dado / Quando / Então`

### 4. Definir contratos de API e fluxos
- Criar templates em `docs/specs/` ou `.ai/templates/api.template.md`.
- Exemplo de contrato:
  - endpoint de busca com parâmetros de perfil, localização e distância
  - resposta com lista de igrejas e coordenadas para mapa

### 5. Criar testes de aceitação e critérios de sucesso
- Usar `docs/specs/` e `.ai/templates/test.template.md`.
- Listar cenários reais:
  - buscar igreja por cidade e perfil
  - aplicar filtro de distância
  - exibir resultados no mapa

### 6. Validar as especificações antes de codificar
- Revisar specs com a equipe ou stakeholders.
- Ajustar critérios e cenários até estarem claros e completos.

### 7. Planejar as entregas por repositório
- Repositório geral: regras, documentação, decisões e especificações.
- Repositório backend: implementação da API, dados e autenticação.
- Repositório frontend: UI, mapas, filtros e integração com a API.

### 8. Implementar em pequenas iterações
- Para cada especificação:
  1. escrever a especificação
  2. definir o contrato de API
  3. escrever testes de aceitação / integração
  4. implementar o comportamento
  5. validar com o cenário definido

### 9. Revisar e refinar continuamente
- A cada iteração, atualizar specs e casos de teste.
- Refatorar o código para manter arquitetura limpa.

## Ordem sugerida para o projeto
1. Especificação de descoberta de igrejas no mapa
2. Especificação de perfis de busca
3. Especificação de filtros de distância e categorias
4. Especificação do contrato de API de busca
5. Especificação de autenticação e perfis de usuário
6. Implementação backend e frontend em paralelo, guiada pelas specs

## Como usar este guia
- Adicione novas specs em `docs/specs/` para cada recurso.
- Use `.ai/templates/` para padronizar documentos.
- Mantenha o repositório central como fonte da verdade do produto.
- Use o backend e frontend apenas para implementar o que as specs definem.

## Benefícios do SDD neste projeto
- Alinhamento entre produto, negócios e implementação.
- Menos retrabalho, pois o comportamento é definido antes do código.
- Melhor comunicação entre os repositórios e times.
- Documentação viva que guia todo o ciclo de desenvolvimento.
