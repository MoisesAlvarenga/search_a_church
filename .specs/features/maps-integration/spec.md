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
| Seleção de API do provedor, esquema de cache e cotas | São decisões de Design, não entregáveis de Specify. |
| Rotas, planejamento de viagem ou navegação passo a passo | Não pertencem ao MVP legado. |

---

## Hipóteses e Questões em Aberto

| Hipótese / decisão | Padrão adotado | Justificativa | Confirmada? |
| ------------------ | -------------- | ------------- | ----------- |
| Provedor de mapas | Google Maps JavaScript API e Google Places são os provedores pretendidos. | Selecionados explicitamente na documentação legada. | Não |
| Resolução de localização | Endereço, cidade e localização atual são entradas válidas; texto não resolvido gera falha explícita. | Descrito pelo MVP e pela especificação legada. | Não |
| Escopo de resultados do provedor | Resultados do provedor são elegíveis ao conjunto de descoberta e devem ser rotulados `maps` com indicação de não cadastrada. | Exigido pelo MVP legado. | Não |
| Identificador e vínculo de localização externa | O `place_id` do Google Maps é a chave externa estável para referenciar a localização física da igreja e vinculá-la a um futuro perfil oficial. | Permite associação determinística entre o local físico e a entidade de perfil. | Não |
| Ponto de partida para criação de perfil | Igrejas não cadastradas no mapa exibem ação ("Reivindicar igreja" / "Criar perfil"), encaminhando dados de localização para `profile-management`. | Facilita a adesão orgânica de novas igrejas na plataforma a partir da busca geográfica. | Não |
| Deduplicação pós-vinculação | Ao criar um perfil vinculado ao `place_id`, o resultado passa a ser rotulado como `app` e o mapa unifica o marcador, evitando duplicatas. | Garante integridade visual e consistência na descoberta. | Não |
| Falha de provedor externo | Resultados cadastrados em `app` permanecem utilizáveis; resultados exclusivos do provedor podem ficar indisponíveis com aviso explícito. | Preserva dados próprios quando a dependência externa falha. | Não |
| Cache e cotas dos resultados do mapa | Não especificados. | Exigem confirmação de produto e operação antes de Design. | Não |

**Questões em aberto:** nenhuma. Todo comportamento não resolvido está registrado como hipótese acima.

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
3. QUANDO uma igreja não cadastrada for selecionada no mapa ou na lista ENTÃO o sistema DEVE exibir os dados disponíveis do provedor (nome, endereço formatado, coordenadas e `place_id`) e DEVE exibir uma opção de ação para "Reivindicar igreja" ou "Criar perfil para esta igreja".
4. QUANDO a ação de criar perfil for acionada a partir de uma igreja não cadastrada do mapa ENTÃO o sistema DEVE encaminhar o contexto geográfico (nome, endereço, coordenadas e `place_id`) para o fluxo de cadastro de perfil de igreja em `profile-management`.
5. QUANDO um perfil de igreja for criado e vinculado a esse `place_id` ENTÃO o sistema DEVE associar permanentemente a localização ao novo perfil oficial, atualizar sua origem para `app` e NÃO DEVE renderizar marcador duplicado para a mesma igreja em buscas futuras.
6. QUANDO o provedor de mapas não puder ser consultado ou retornar erro ENTÃO o sistema DEVE informar a indisponibilidade dos dados externos sem afetar a exibição nem o funcionamento das igrejas cadastradas em `app`.

**Teste Independente**: Executar busca em área com igrejas cadastradas e igrejas presentes apenas no Google Maps. Verificar a exibição diferenciada das igrejas do mapa com ação de criar/reivindicar perfil, simular a transmissão do `place_id` para cadastro e comprovar a deduplicação pós-vinculação.

## Casos de Borda

- QUANDO marcadores do mapa se sobrepuserem no nível de zoom atual ENTÃO o sistema DEVE manter cada resultado acessível através de agrupamento ou expansão visual.
- QUANDO resultado do Google Maps corresponder a uma igreja já cadastrada (mesmo `place_id` vinculado) ENTÃO o sistema DEVE exibir o perfil oficial cadastrado em `app` e NÃO DEVE exibir o resultado não cadastrado em duplicidade.
- QUANDO dois usuários tentarem iniciar a criação de perfil para a mesma igreja do Google Maps simultaneamente ENTÃO o sistema DEVE garantir a integridade dos dados e impedir perfis duplicados para o mesmo `place_id`.
- QUANDO a igreja no Google Maps contiver dados parciais (ex.: sem número predial ou horário de culto) ENTÃO o sistema DEVE exibir os dados disponíveis e orientar a criação do perfil para complementar as informações eclesiásticas.
- QUANDO o usuário negar acesso à localização atual ENTÃO o sistema DEVE permitir informar endereço ou cidade.

## Rastreabilidade de Requisitos

| ID do Requisito | História | Fase | Status |
| --------------- | -------- | ---- | ------ |
| MAP-01 | P1: Visualizar Resultados de Descoberta no Mapa | Specify | Pendente |
| MAP-02 | P1: Visualizar Resultados de Descoberta no Mapa | Specify | Pendente |
| MAP-03 | P1: Conectar Seleção do Mapa e da Lista | Specify | Pendente |
| MAP-04 | P2: Descobrir Igrejas no Google Maps e Permitir Vinculação a Novo Perfil | Specify | Pendente |
| MAP-05 | P2: Descobrir Igrejas no Google Maps e Permitir Vinculação a Novo Perfil | Specify | Pendente |
| MAP-06 | P2: Descobrir Igrejas no Google Maps e Permitir Vinculação a Novo Perfil | Specify | Pendente |
| MAP-07 | P2: Descobrir Igrejas no Google Maps e Permitir Vinculação a Novo Perfil | Specify | Pendente |

**Cobertura:** 7 no total, 0 mapeados para tarefas, 7 não mapeados aguardando confirmação da especificação.

## Critérios de Sucesso

- [ ] Usuários conseguem ver os resultados mapeáveis e a localização pesquisada em uma única visão de mapa.
- [ ] Selecionar igreja mapeável pela lista ou pelo mapa identifica consistentemente o mesmo resultado.
- [ ] Igrejas do Google Maps não cadastradas são visíveis com rótulo `maps` e opção acessível para iniciar criação/vinculação de perfil.
- [ ] A localização e o identificador do provedor (`place_id`) são preservados e associados com sucesso ao perfil criado pela igreja.
- [ ] Igrejas vinculadas a perfis oficiais não produzem marcadores duplicados no mapa.
- [ ] Indisponibilidades do provedor não ocultam resultados cadastrados disponíveis.