# Google Maps APIs e Playgrounds

## O que você precisa para começar

1. Conta Google
   - Use uma conta Google válida para acessar o Google Cloud Platform.

2. Projeto no Google Cloud
   - Crie um projeto no console do Google Cloud: https://console.cloud.google.com/
   - O projeto é o contêiner para suas APIs, chaves e faturamento.

3. Ativar faturamento
   - O Google Maps Platform exige faturamento ativo no projeto.
   - Há um crédito gratuito mensal para uso inicial.
   - Não use a conta sem configurar o faturamento, pois as APIs não funcionarão.

4. Habilitar APIs necessárias
   - `Maps JavaScript API`
   - `Places API` ou `Places Library` para consulta de locais
   - `Geocoding API` para converter endereços em coordenadas
   - `Directions API` e `Distance Matrix API` se precisar de rotas ou distâncias detalhadas
   - `Maps Static API` se precisar de imagens estáticas de mapas

5. Criar credenciais
   - No console do Google Cloud, vá em `APIs e serviços > Credenciais`
   - Crie uma chave de API (`API key`)
   - Configure restrições:
     - restrição por referenciador HTTP (domínio) ou por IP para maior segurança
     - habilitar apenas as APIs necessárias

6. Configuração no frontend
   - Armazene a chave de API em variáveis de ambiente ou em configuração segura
   - Use a chave apenas no frontend com restrição de referenciador
   - Se for necessário um backend proxy, faça requisições ao Google Maps do servidor para proteger a chave

## Ferramentas e playgrounds para se familiarizar

### Google Cloud Console
- Painel principal para criar projetos, habilitar APIs e gerenciar credenciais.
- Acesse: https://console.cloud.google.com/

### Google Maps Platform Documentation
- Documentação oficial para cada API.
- `Maps JavaScript API`: https://developers.google.com/maps/documentation/javascript
- `Places API`: https://developers.google.com/maps/documentation/places/web-service/overview
- `Geocoding API`: https://developers.google.com/maps/documentation/geocoding/overview

### APIs Explorer
- Playground oficial para testar requisições HTTP das APIs.
- Acesse em cada docs ou diretamente:
  - https://developers.google.com/apis-explorer/
- Permite ver parâmetros, enviar requisições e examinar respostas.

### Google Maps Platform Sample Code
- Exemplos de uso de JavaScript no site oficial.
- Use exemplos de `Maps JavaScript API` para aprender a carregar mapas, marcadores e infowindows.
- Consulte o guia de `Places Library` para buscar locais e mostrar resultados.

### Google Cloud Shell / SDK (opcional)
- Para gerenciar o projeto via linha de comando.
- Útil se você quiser automatizar a criação de chaves ou configurações.
- Mais informações: https://cloud.google.com/shell

## Ferramentas específicas recomendadas

- **Google Maps JavaScript API Playground**
  - Há exemplos interativos na documentação que podem ser copiados e testados diretamente.
- **Places API Test**
  - Use o `Place Search` e `Place Details` nos exemplos para entender os dados retornados.
- **Geocoding API Test**
  - Teste conversões de endereço e localização com os exemplos da documentação.

## Recomendações práticas

- Comece com uma chave de API restrita para seu domínio local (`localhost`) durante o desenvolvimento.
- Teste primeiro no developer console do Google Maps e depois integre ao Angular.
- Documente a chave e o projeto usado para evitar confusão entre ambientes.
- Monitore o uso no console para não ultrapassar limites.

## Resumo rápido

Para usar Google Maps no projeto você precisa:
- Conta Google
- Projeto no Google Cloud
- Faturamento ativado
- APIs habilitadas
- Chave de API restrita
- Playground via docs e APIs Explorer

Isso lhe dá o ambiente ideal para experimentar, validar e depois integrar o Google Maps com o frontend Angular do projeto `search a church`.
