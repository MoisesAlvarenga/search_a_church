# Checklist de setup do Google Maps

## 1. Configuração inicial
- [ ] Criar conta Google ou usar conta existente.
- [ ] Criar um projeto no Google Cloud Console.
- [ ] Ativar faturamento para o projeto.
- [ ] Habilitar as APIs necessárias:
  - Maps JavaScript API
  - Places API / Places Library
  - Geocoding API
  - (Opcional) Directions API / Distance Matrix API
  - (Opcional) Maps Static API

## 2. Criar credenciais
- [ ] Criar chave de API no Google Cloud Console.
- [ ] Definir restrições para a chave:
  - restrição por referenciador HTTP para frontend
  - ou restrição por IP/rede para backend
- [ ] Habilitar apenas as APIs que serão usadas com essa chave.

## 3. Configurar o frontend local
- [ ] Adicionar a chave de API em variáveis de ambiente de desenvolvimento.
- [ ] Configurar o carregamento do Google Maps JS no Angular.
- [ ] Testar o mapa básico em `localhost` antes de adicionar lógica de negócio.

## 4. Testar no playground e na documentação
- [ ] Usar o Google APIs Explorer para testar chamadas às APIs.
- [ ] Testar buscas no Places API e geocodificação no console.
- [ ] Validar o formato de resposta e os campos necessários para o app.

## 5. Integrar ao app
- [ ] Criar componente/mapa com Google Maps JS API.
- [ ] Exibir marcadores e infowindows.
- [ ] Usar Places API para buscar igrejas não cadastradas.
- [ ] Permitir pesquisa por endereço e por localização atual.
- [ ] Exibir origem `app` ou `maps` nos resultados.

## 6. Segurança e produção
- [ ] Definir chave de API separada para produção.
- [ ] Aplicar restrições de domínio para a chave de produção.
- [ ] Revisar quotas e limites de uso no Google Cloud Console.
- [ ] Monitorar consumo e custos.

## 7. Opções de arquitetura: frontend direto vs proxy backend

### Usar a API diretamente no frontend
- Vantagens:
  - implementação mais simples e rápida.
  - ideal para acesso ao `Maps JavaScript API`, `Places Library` e geocodificação leve.
- Desvantagens:
  - a chave de API fica exposta ao cliente, mesmo com restrições.
  - menos controle sobre uso e menos proteção contra abusos.

### Criar um proxy no backend
- Vantagens:
  - mantém a chave de API oculta no servidor.
  - permite aplicar regras adicionais, caching e limites de uso.
  - facilita integrar várias fontes de dados antes de retornar ao frontend.
- Desvantagens:
  - aumenta a complexidade do backend.
  - pode exigir mais manutenção e infraestrutura.

## Recomendação
- Para o `Search a Church`, use **Google Maps JS API diretamente no frontend** para renderizar o mapa e exibir marcadores.
- Use **proxy backend** quando:
  - precisar proteger uma chave do Places Web Service ou Geocoding API mais sensível,
  - quiser aplicar caching ou regras de negócios antes de chamar o Google,
  - desejar manter controle total do fluxo de dados e evitar exposição de chaves.

## Resumo prático
- Use frontend direto para o mapa interativo e a maior parte das chamadas de `Places`/`Geocoding` com chave restrita.
- Use backend proxy para chamadas sensíveis ou quando precisar unificar dados de app e Google antes de entregar ao cliente.
