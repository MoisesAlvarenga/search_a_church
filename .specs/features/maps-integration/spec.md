# Especificação de Integração com Mapas

## Problema

A descoberta baseada em localização é difícil de avaliar apenas por uma lista, e muitas igrejas não estarão cadastradas no produto. O produto precisa de mapa interativo para visualizar resultados e complementar dados cadastrados com igrejas localizadas no Google Maps, permitindo que templos ainda não cadastrados sejam encontrados e que, futuramente, seus representantes possam criar um perfil oficial vinculado à localização mapeada.

## Objetivos

- [ ] Resultados de busca podem ser visualizados como marcadores georreferenciados em mapa interativo.
- [ ] Interações na lista e no mapa identificam o mesmo resultado de igreja.
- [ ] A descoberta pode incluir igrejas provenientes do Google Maps e não cadastradas no produto.
- [ ] Igrejas descobertas pelo Google Maps possuem identificador estável de localização que permite sua futura vinculação ao perfil criado pela igreja.

## Fora do Escopo

| Funcionalidade | Motivo |
| -------------- | ------ |
| Definição do ranking de busca | Definida em `search-discovery`. |
| Gestão e validação de regras de perfis de igreja | Definida em `profile-management`. |
| Autenticação e controle de sessão | Definida em `authentication-authorization`. O acesso ao mapa exige usuário logado. |
| Seleção de API do provedor, esquema de cache e cotas | São decisões de Design, não entregáveis de Specify. |
| Rotas, planejamento de viagem ou navegação passo a passo | Não pertencem ao MVP legado. |

---

## Hipóteses e Questões em Aberto

| Hipótese / decisão | Padrão adotado | Justificativa | Confirmada? |
| ------------------ | -------------- | ------------- | ----------- |
| Provedor de mapas | Google Maps Platform (Google Maps SDK/JavaScript API para renderização vetorial e Google Places API / Geocoding para busca e resolução de locais). | Confirmado em AD-025: padrão da indústria com ampla cobertura global e dados geográficos consolidados. | Sim |
| Autenticação para acesso ao mapa | Acesso à busca e visualização do mapa (`GET`) exige usuário cadastrado e autenticado via JWT. Não é público. | Confirmado em AD-007 e AD-025: proteger recursos do mapa e engajar usuários cadastrados. | Sim |
| Resolução de localização | Coordenadas do GPS nativo do dispositivo e entradas textuais (endereço, bairro, cidade) via Google Geocoding / Places Autocomplete. Texto não resolvido gera mensagem explícita e amigável sem renderizar marcadores falsos. | Confirmado em AD-025: assegura precisão espacial e clareza ao usuário quando o local não for encontrado. | Sim |
| Escopo de resultados do provedor | Resultados provenientes do Google Places entram na lista e no mapa rotulados como `origem: maps` com indicativo visual explícito de "Não cadastrada" / "Não reivindicada". | Confirmado em AD-025: amplia a densidade inicial de templos sem confundir dados oficiais do app com dados brutos externos. | Sim |
| Identificador e vínculo de localização externa | O `place_id` do Google Maps é a chave externa estável determinística para identificar o ponto físico e vincular a igreja a um futuro perfil oficial. | Confirmado em AD-004 e AD-025: associação determinística e perene entre o local físico e a entidade de perfil. | Sim |
| Ponto de partida para criação de perfil / reivindicação | Igrejas com `origem: maps` exibem ação destacada ("Reivindicar esta igreja"), encaminhando nome, endereço, coordenadas e `place_id` pré-preenchidos para `church-profile-claim`. | Confirmado em AD-025: viabiliza adesão orgânica ágil e sem atrito a partir da experiência de mapa. | Sim |
| Deduplicação pós-vinculação (App-First) | Ao existir igreja cadastrada com o mesmo `place_id`, o mapa renderiza exclusivamente o marcador da base oficial (`origem: app`), descartando o resultado redundante do Google Maps. | Confirmado em AD-023 e AD-025: integridade visual sem pinos duplicados para a mesma igreja física. | Sim |
| Falha de provedor externo (Degradação graciosa) | Falhas ou esgotamento de cotas na API do Google Maps mantêm o funcionamento normal de todas as igrejas da base local (`origem: app`), apresentando aviso informativo sutil e não bloqueante ao usuário. | Confirmado em AD-025: resiliência operacional contínua independente da dependência externa. | Sim |
| Cache e cotas dos resultados do mapa | Backend armazena dados de Places em cache com TTL de até 30 dias (place_id persistido indefinidamente conforme ToS do Google); cliente aplica debounce de 500ms nas interações de arraste e zoom antes de disparar novas consultas. | Confirmado em AD-025: otimização de performance, respeito às cotas e conformidade legal com a Google Maps Platform. | Sim |

**Questões em aberto:** nenhuma. Todas as 9 hipóteses da especificação de integração com mapas estão integralmente confirmadas pelo responsável do produto.

---

## Histórias de Usuário

### P1: Visualizar Resultados de Descoberta no Mapa MVP

**História de Usuário**: Como pessoa em busca de igreja, quero visualizar igrejas compatíveis em mapa interativo para entender sua relação geográfica com a localização pesquisada.

**Por que P1**: A visualização no mapa é resultado central declarado pelo MVP.

**Critérios de Aceitação**:

1. QUANDO uma busca de descoberta retornar resultados com posições geográficas ENTÃO o sistema DEVE renderizar marcador para cada resultado mapeável.
2. QUANDO o mapa exibir um conjunto de resultados pela primeira vez ENTÃO o sistema DEVE enquadrar a localização pesquisada e os marcadores disponíveis na área visível.
3. QUANDO um resultado não tiver posição geográfica ENTÃO o sistema DEVE mantê-lo disponível na lista e NÃO DEVE renderizar marcador enganoso.
4. QUANDO não houver resultados mapeáveis ENTÃO o sistema DEVE exibir estado explícito de mapa vazio.

**Teste Independente**: Executar busca com resultados mapeáveis e não mapeáveis e verificar marcadores, enquadramento e comportamento da lista.

---

### P1: Conectar Seleção do Mapa e da Lista MVP

**História de Usuário**: Como pessoa em busca de igreja, quero selecionar uma igreja pelo mapa ou pela lista ranqueada para inspecionar o mesmo resultado no formato que preferir.

**Por que P1**: A especificação legada exige interação sincronizada entre lista e mapa.

**Critérios de Aceitação**:

1. QUANDO um usuário selecionar um marcador do mapa ENTÃO o sistema DEVE mostrar resumo identificador da igreja, incluindo nome, endereço, distância quando disponível, resumo de avaliação quando disponível e origem.
2. QUANDO um usuário selecionar resultado na lista ranqueada ENTÃO o sistema DEVE focalizar o marcador correspondente, quando o resultado for mapeável.
3. QUANDO um usuário selecionar marcador do mapa ENTÃO o sistema DEVE identificar o resultado correspondente na lista ranqueada.

**Teste Independente**: Selecionar a mesma igreja pelo marcador e pela lista e verificar a identificação sincronizada.

---

### P2: Descobrir Igrejas no Google Maps e Permitir Vinculação a Novo Perfil

**História de Usuário**: 
- Como pessoa em busca de igreja, quero visualizar igrejas localizadas no Google Maps mesmo que ainda não cadastradas no produto, para encontrar templos em qualquer região pesquisada.
- Como representante de uma igreja localizada no Google Maps, quero poder iniciar o cadastro do perfil oficial a partir da sua localização no mapa, para que a localização e a referência do Google Maps fiquem vinculadas ao perfil da igreja.

**Por que P2**: Amplia a densidade de igrejas disponíveis no produto e viabiliza a transição orgânica de dados externos para perfis oficiais verificados.

**Critérios de Aceitação**:

1. QUANDO o provedor de mapas retornar igreja na área pesquisada que ainda não possua perfil no produto ENTÃO o sistema DEVE exibi-la no mapa com marcador e na lista de descoberta como resultado elegível.
2. QUANDO uma igreja exibida for proveniente exclusivamente do Google Maps ENTÃO o sistema DEVE rotular sua origem como `maps` e indicar explicitamente o estado de "Não cadastrada" (ou não reivindicada).
3. QUANDO uma igreja não cadastrada for selecionada no mapa ou na lista ENTÃO o sistema DEVE exibir os dados disponíveis do provedor (nome, endereço formatado, coordenadas e `place_id`) e DEVE exibir uma opção de ação para "Reivindicar esta igreja".
4. QUANDO a ação de reivindicar igreja for acionada a partir de uma congregação não cadastrada do mapa ENTÃO o sistema DEVE encaminhar o contexto geográfico (nome, endereço, coordenadas e `place_id`) pré-preenchido para o fluxo de reivindicação em `church-profile-claim`.
5. QUANDO um perfil de igreja for homologado e vinculado a esse `place_id` ENTÃO o sistema DEVE associar permanentemente a localização ao novo perfil oficial, atualizar sua origem para `app` e NÃO DEVE renderizar marcador duplicado para a mesma igreja em buscas futuras, aplicando a regra App-First.
6. QUANDO o provedor de mapas não puder ser consultado ou retornar erro (HTTP 5xx, timeout ou cota esgotada) ENTÃO o sistema DEVE entrar em modo de degradação graciosa, exibindo aviso informativo discreto e mantendo o funcionamento normal e sem bloqueio das congregações cadastradas em `app`.
7. QUANDO o usuário interagir com o mapa via arraste (*pan*) ou zoom ENTÃO o sistema DEVE aplicar debounce de 500ms antes de disparar novas consultas ao provedor externo, otimizando o consumo de cotas.
8. QUANDO dados de locais forem obtidos da Google Places API ENTÃO o backend DEVE armazená-los em cache temporário com TTL de até 30 dias para otimização de requisições, retendo o `place_id` de forma duradoura.

**Teste Independente**: Executar busca em área com igrejas cadastradas e igrejas presentes apenas no Google Maps. Verificar a exibição diferenciada das igrejas do mapa com ação de reivindicar perfil, simular a transmissão do `place_id` para `church-profile-claim` e comprovar a deduplicação App-First pós-vinculação e o comportamento em falha simulada da API externa.

## Casos de Borda

- QUANDO marcadores do mapa se sobrepuserem no nível de zoom atual ENTÃO o sistema DEVE manter cada resultado acessível através de agrupamento ou expansão visual.
- QUANDO resultado do Google Maps corresponder a uma igreja já cadastrada (mesmo `place_id` vinculado) ENTÃO o sistema DEVE exibir o perfil oficial cadastrado em `app` e NÃO DEVE exibir o resultado não cadastrado em duplicidade.
- QUANDO dois usuários tentarem iniciar a reivindicação para a mesma igreja do Google Maps simultaneamente ENTÃO o sistema DEVE garantir a integridade dos dados e impedir reivindicações concorrentes inválidas para o mesmo `place_id` conforme as regras de concorrência de `church-profile-claim`.
- QUANDO a igreja no Google Maps contiver dados parciais (ex.: sem número predial ou horário de culto) ENTÃO o sistema DEVE exibir os dados disponíveis e orientar a complementação das informações no perfil.
- QUANDO o usuário negar acesso à localização atual ou a busca textual não puder ser resolvida geograficamente ENTÃO o sistema DEVE exibir mensagem amigável padronizada ("Localização não encontrada") e permitir nova busca manual, sem renderizar marcadores falsos.

## Rastreabilidade de Requisitos

| ID do Requisito | História | Fase | Status |
| --------------- | -------- | ---- | ------ |
| MAP-01 | P1: Visualizar Resultados de Descoberta no Mapa (Marcadores e Enquadramento - AD-025) | Specify | Confirmado |
| MAP-02 | P1: Resolução de Localização e Tratamento de Erros Amigáveis (GPS e Texto - AD-025) | Specify | Confirmado |
| MAP-03 | P1: Conectar Seleção do Mapa e da Lista Ranqueada (Sincronização Bidirecional) | Specify | Confirmado |
| MAP-04 | P2: Descobrir Igrejas no Google Maps com Rótulo `origem: maps` (Não Cadastrada - AD-025) | Specify | Confirmado |
| MAP-05 | P2: Ponto de Entrada para Reivindicação de Perfil (CTA com Pré-preenchimento para Claim - AD-025) | Specify | Confirmado |
| MAP-06 | P2: Deduplicação App-First no Mapa e Unificação de Marcadores (AD-023 e AD-025) | Specify | Confirmado |
| MAP-07 | P2: Resiliência Graciosa, Cache de Places (30 dias) e Debounce de 500ms (AD-025) | Specify | Confirmado |

**Cobertura:** 7 requisitos estruturados, 7 confirmados com critérios BDD e decisões arquiteturais vinculadas, 0 pendentes de especificação. Prontos para Design.

## Critérios de Sucesso

- [ ] Usuários conseguem ver os resultados mapeáveis e a localização pesquisada em uma única visão de mapa.
- [ ] Selecionar igreja mapeável pela lista ou pelo mapa identifica consistentemente o mesmo resultado.
- [ ] Igrejas do Google Maps não cadastradas são visíveis com rótulo `maps` e opção acessível para iniciar a reivindicação do perfil oficial.
- [ ] A localização e o identificador do provedor (`place_id`) são preservados e associados com sucesso ao perfil criado pela igreja.
- [ ] Igrejas vinculadas a perfis oficiais não produzem marcadores duplicados no mapa (App-First).
- [ ] Indisponibilidades do provedor não ocultam resultados cadastrados disponíveis, degradando graciosamente com aviso discreto.
- [ ] Consultas de mapa utilizam cache em backend (até 30 dias) e debounce de 500ms para otimização de cotas.