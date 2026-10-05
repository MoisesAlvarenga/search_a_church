# Especificação de Descoberta de Igrejas

## Problema

Viajantes e pessoas em processo de mudança precisam encontrar igrejas relevantes para suas preferências e localização sem conhecer a região. O produto deve tornar compreensível o fluxo principal de descoberta: buscar a partir de uma localização, comparar resultados ranqueados e distinguir dados cadastrados no produto de dados obtidos pelo mapa.

## Objetivos

- [ ] Um usuário consegue obter uma lista ordenada de igrejas, considerando localização e preferências de busca disponíveis.
- [ ] Cada resultado expõe informações suficientes para comparar opções e identificar sua fonte de dados.
- [ ] O fluxo de busca principal do MVP pode ser concluído em até três ações: informar localização, informar preferências opcionalmente e visualizar resultados.

## Fora do Escopo

| Funcionalidade | Motivo |
| -------------- | ------ |
| Renderização do mapa e interação com marcadores | Definida de forma independente em `maps-integration`. |
| Criação ou edição de perfis | Definida de forma independente em `profile-management`. |
| Criação de avaliações | Definida de forma independente em `reviews-feedback`. |
| Autenticação e gestão de sessão | Definida de forma independente em `authentication-authorization`. |
| Recomendações personalizadas a partir de comportamento histórico | Explicitamente fora do MVP legado. |

---

## Hipóteses e Questões em Aberto

| Hipótese / decisão | Padrão adotado | Justificativa | Confirmada? |
| ------------------ | -------------- | ------------- | ----------- |
| Significado e modo de `radiusKm` | Modelo Híbrido por padrão (`hybrid`), com seletor opcional para o usuário escolher entre Rígido (`strict` / hard cut-off) e Suave (`flexible` / soft factor). | Confirmado em AD-018: no modo híbrido, atua como filtro rígido padrão, expandindo automaticamente caso não haja resultados no raio e notificando o usuário com metadados explicativos. | Sim |
| Ranqueamento padrão e desempate | Ordenação por `matchScore` decrescente com cadeia determinística de desempate: 1º volume de avaliações decrescente, 2º distância física crescente e 3º nome alfabético crescente. | Confirmado em AD-019: prioriza prova social e engajamento comunitário antes da distância quando há notas de relevância idênticas, fechando com desempate determinístico por nome. | Sim |
| Fórmula de pontuação e pesos de relevância | Ponderação multidimensional normalizada em [0, 100]: 40% Proximidade Física, 45% Afinidade de Preferências (denominação, idioma, estilo de culto) e 15% Reputação Comunitária. Concessão de baseline neutra (3.0 de 5.0) para congregações recém-cadastradas sem avaliações (regra de cold start). | Confirmado em AD-020: equilibra conveniência geográfica de deslocamento, alinhamento teológico e prova social, prevenindo invisibilidade de igrejas novas. | Sim |
| Precedência de preferências e buscas sem perfil | As preferências cadastradas no perfil atuam como baseline padrão da busca; filtros manuais aplicados na consulta realizam override granular efêmero restrito à requisição. Veda-se a opção de salvar alterações na busca (exclusivo do perfil). Buscas sem preferências ativas recebem nota neutra na afinidade. | Confirmado em AD-021: garante personalização automática inicial e flexibilidade temporária para buscas circunstanciais, mantendo a gestão de perfil rigorosamente segregada em `profile-management`. | Sim |
| Limites de resultados e paginação | Paginação baseada em cursor (*keyset pagination*) estruturada sobre a tupla determinística de AD-019, com 20 congregações por página e teto máximo global de 100 congregações por consulta no raio. | Confirmado em AD-022: garante estabilidade de scroll infinito sem inconsistências e viabiliza previsibilidade operacional de carga, orientando o usuário a refinar a busca ao atingir o teto de 100 registros. | Sim |

**Questões em aberto:** nenhuma. Todo comportamento legado não resolvido está registrado como hipótese acima.

---

## Histórias de Usuário

### P1: Encontrar Igrejas por Localização e Preferências MVP

**História de Usuário**: Como viajante ou pessoa em mudança, quero buscar a partir de uma localização com minhas preferências disponíveis para encontrar igrejas relevantes para minha situação.

**Por que P1**: Esta é a proposta de valor central do MVP.

**Critérios de Aceitação**:

1. QUANDO um usuário informar uma localização válida para busca ENTÃO o sistema DEVE retornar resultados de igrejas associados àquela localização.
2. QUANDO um usuário autenticado possuir preferências no perfil ENTÃO o sistema DEVE carregá-las automaticamente como baseline padrão para cálculo de relevância.
3. QUANDO um usuário aplicar filtros manuais na busca ENTÃO o sistema DEVE aplicar override granular efêmero sobre as preferências do perfil exclusivamente para aquela consulta, NÃO DEVE persistir as alterações no perfil do usuário e NÃO DEVE exibir opção de salvar preferências na interface de busca.
4. QUANDO um usuário não possuir preferências no perfil e não informar filtros diretos ENTÃO o sistema DEVE atribuir pontuação neutra de baseline à afinidade teológica (45%), ranqueando os resultados primariamente por proximidade (40%) e reputação (15%), sem rejeitar a busca por ausência de preferências.
5. QUANDO uma localização não puder ser resolvida para uma área geográfica de busca ENTÃO o sistema DEVE informar que a localização é inválida ou não foi resolvida e NÃO DEVE apresentá-la como busca bem-sucedida.

**Teste Independente**: Buscar a partir de uma localização conhecida com e sem preferências e verificar que os resultados são retornados ou que a falha de localização é explícita.

---

### P1: Comparar Resultados Ranqueados MVP

**História de Usuário**: Como pessoa em busca de igreja, quero resultados ordenados por relevância e explicados com informações essenciais para comparar igrejas rapidamente.

**Por que P1**: Uma lista de igrejas sem ranqueamento compreensível não resolve o problema de descoberta.

**Critérios de Aceitação**:

1. QUANDO uma busca retornar mais de um resultado ENTÃO o sistema DEVE apresentar os resultados ordenados por relevância (`matchScore` decrescente).
2. QUANDO dois ou mais resultados obtiverem a mesma pontuação de relevância (`matchScore`) ENTÃO o sistema DEVE aplicar a cadeia determinística de desempate confirmada em AD-019 nesta ordem estrita:
   - 1º: Maior volume de avaliações (`review_count` decrescente).
   - 2º: Menor distância física estimada (`distance_km` crescente).
3. QUANDO o sistema calcular a pontuação de relevância (`matchScore`) ENTÃO DEVE aplicar a fórmula ponderada normalizada na escala [0, 100] confirmada em AD-020:
   - **Proximidade Física (40% do peso):** decaimento inversamente proporcional à distância física até o ponto de referência de busca.
   - **Afinidade de Preferências (45% do peso):** compatibilidade entre as preferências ativas (denominação/linha teológica, idioma do culto e estilo litúrgico) e os atributos cadastrais da congregação.
   - **Reputação Comunitária (15% do peso):** nota média ponderada das avaliações recebidas; para congregações recém-cadastradas sem avaliações (*cold start*), o sistema DEVE atribuir pontuação neutra de baseline (equivalente a 3.0 / 5.0 estrelas) para não penalizar igrejas novas na descoberta.
4. QUANDO um resultado for exibido ENTÃO DEVE incluir nome, endereço, posição geográfica, distância estimada, horário de culto quando conhecido, atributos de perfil quando conhecidos, tipo de culto quando conhecido, pontuação de relevância, resumo de avaliação quando conhecido, status de cadastro e origem.
5. QUANDO dois resultados forem provenientes de fontes diferentes ENTÃO o sistema DEVE avaliá-los sob a mesma política de ranqueamento por relevância.
6. QUANDO a consulta de descoberta retornar registros ENTÃO o sistema DEVE fornecer os resultados paginados em lotes de no máximo 20 congregações por página via paginação baseada em cursor (*keyset pagination*), acompanhados dos metadados de controle `next_cursor` e `has_next_page`.
7. QUANDO a paginação acumular 100 congregações retornadas para uma mesma consulta no raio informado ENTÃO o sistema DEVE encerrar a paginação determinando `has_next_page: false` e `next_cursor: null`, incluindo sinalização explicativa na resposta orientando o usuário a reduzir o raio de busca (`radiusKm`) ou aplicar filtros adicionais.

**Teste Independente**: Buscar uma localização com igrejas de pontuações de relevância idênticas e verificar se o critério de desempate é rigorosamente obedecido (maior volume de avaliações primeiro, depois menor distância e por fim ordem alfabética); verificar se as páginas retornam lotes de 20 itens com cursor determinístico e se o fluxo cessa a paginação ao atingir o teto de 100 congregações.

---

### P2: Filtrar uma Busca de Descoberta

**História de Usuário**: Como pessoa em busca de igreja, quero restringir resultados por idioma, tipo de culto e distância desejada para que o ranqueamento reflita restrições práticas.

**Por que P2**: Filtros melhoram a relevância, mas não são necessários para demonstrar o fluxo básico de busca.

**Critérios de Aceitação**:

1. QUANDO um usuário informar uma preferência de idioma ENTÃO o sistema DEVE aplicá-la como entrada para avaliação de relevância.
2. QUANDO um usuário informar uma preferência de tipo de culto ENTÃO o sistema DEVE aplicá-la como entrada para avaliação de relevância.
3. QUANDO um usuário informar uma distância desejada (`radiusKm`) sem especificar o modo ENTÃO o sistema DEVE aplicar o modo Híbrido (`hybrid`) por padrão: filtrar estritamente as igrejas dentro de `radiusKm`; se nenhum resultado for encontrado dentro do raio informado, o sistema DEVE expandir automaticamente a busca para uma faixa maior e sinalizar a resposta com metadados explicativos (`is_expanded_radius: true`, `original_radius_km` e `applied_radius_km`) para apresentação de aviso amigável na interface.
4. QUANDO um usuário selecionar explicitamente o modo de corte rígido (`strict` / hard cut-off) ENTÃO o sistema DEVE restringir os resultados estritamente ao raio `radiusKm`, retornando lista vazia se nenhuma igreja estiver localizada dentro daquele limite.
5. QUANDO um usuário selecionar explicitamente o modo suave (`flexible` / soft factor) ENTÃO o sistema DEVE utilizar a distância como ponderação no ranqueamento, exibindo resultados que excedam `radiusKm` com pontuação penalizada pela distância.
6. QUANDO um valor de filtro estiver malformado ou fora dos limites aceitos ENTÃO o sistema DEVE rejeitá-lo com um resultado de validação claro.

**Teste Independente**: Executar buscas com raio em modo padrão (híbrido) verificando a expansão automática em áreas sem resultados imediatos, testar buscas em modo `strict` verificando a omissão de registros fora do raio, e testar em modo `flexible` verificando a presença de resultados além do raio com pontuação proporcional.

---

### P2: Identificar a Origem dos Dados

**História de Usuário**: Como pessoa em busca de igreja, quero saber se um resultado está cadastrado no produto ou vem de um provedor de mapas para avaliar o contexto das informações.

**Por que P2**: O MVP legado inclui explicitamente os dois tipos de resultados.

**Critérios de Aceitação**:

1. QUANDO um resultado tiver origem em um cadastro de igreja do produto ENTÃO o sistema DEVE rotular sua origem como `app`.
2. QUANDO um resultado tiver origem no fluxo de descoberta por provedor de mapas ENTÃO o sistema DEVE rotular sua origem como `maps`.
3. QUANDO informações de origem não estiverem disponíveis ENTÃO o sistema NÃO DEVE rotular o resultado como `app` ou `maps` sem evidência.

**Teste Independente**: Visualizar um resultado cadastrado e outro proveniente de provedor e verificar seus rótulos de origem.

## Casos de Borda

- QUANDO nenhuma igreja corresponder a uma localização pesquisável ENTÃO o sistema DEVE retornar um estado explícito de resultado vazio.
- QUANDO um campo opcional do resultado não estiver disponível ENTÃO o sistema DEVE preservar o resultado e representar o campo como indisponível, sem inventar valor.
- QUANDO registros duplicados representarem a mesma igreja física em fontes diferentes (`app` e `maps`) ENTÃO o sistema DEVE aplicar a política de deduplicação *App-First* confirmada em AD-023: identificar duplicidade primariamente pelo `place_id` (e subsidiariamente por distância < 50m com alta similaridade léxica de nome), unificar o registro sob `origem: app` (com badge de verificação se congregação verificada), enriquecer com atributos complementares não conflitantes do mapa e descartar o item redundante de `maps` para apresentar um único resultado ao usuário.
- QUANDO dados de feedback não estiverem disponíveis ENTÃO o sistema DEVE preservar o resultado e representar o resumo de avaliação como indisponível.

## Dimensões de Requisitos Implícitos (Sweep)

| Dimensão | Cobertura na Especificação |
| -------- | -------------------------- |
| **Validação de Entrada e Limites** | Validação de coordenadas geográficas (latitude em [-90, 90], longitude em [-180, 180]); raio de busca estritamente positivo (`radiusKm > 0`); sanitização de filtros textuais; tamanho de página fixado em 20 itens por requisição e teto global de 100 congregações por consulta de busca (AD-022). |
| **Estados de Falha e Parciais** | Retorno de erro estruturado explícito para coordenadas não resolvidas ou inválidas; estado de resultado vazio amigável quando nenhuma congregação for encontrada mesmo após expansão híbrida (AD-018); preservação de registros com campos opcionais indisponíveis (atributos ausentes são sinalizados como indisponíveis sem quebrar a resposta). |
| **Idempotência, Deduplicação e Precedência** | Operação de consulta idempotente (GET com cursor); política de deduplicação *App-First* ancorada no `place_id` determinístico e subsidiariamente por raio < 50m com similaridade léxica de nome, eliminando duplicatas visuais e sobrepondo registros locais com enriquecimento complementar de mapas (AD-004 e AD-023). |
| **Fronteiras de Autenticação e Rate Limits** | Acesso à busca e descoberta no mapa exige autenticação prévia via JWT sem estado (AD-006 e AD-007); proteção de rate limit nos endpoints de busca via Redis para contenção de abusos e previsibilidade de custos de provedores externos. |
| **Concorrência e Ordenação** | Ordenação estritamente determinística e imune a race conditions através de cadeia de 4 níveis de desempate (`matchScore` DESC → `review_count` DESC → `distance_km` ASC → `name` ASC conforme AD-019); paginação baseada em cursor (*keyset pagination*) imune a desvios de inserção em tempo real durante o scroll infinito (AD-022). |
| **Ciclo de Vida de Dados e Freshness** | Respeito estrito aos termos de uso do Google Maps Platform (não persistência permanente de dados proibidos de Places além do `place_id`); expiração de cache espacial de descoberta externa; segregação estrita entre filtros de busca e persistência de perfil (AD-021). |
| **Observabilidade e Telemetria** | Registro de métricas de busca: volume de consultas, distribuição de latência, taxa de expansão automática de raio (`is_expanded_radius: true`), consultas com zero resultados e distribuição de `matchScore` para calibração contínua do algoritmo. |
| **Integridade de Transição e Regras de Negócio** | Aplicação estrita da semântica de raio (Híbrido padrão, Rígido e Suave sob AD-018); precedência de preferências (baseline do perfil + override efêmero na busca sob AD-021); fórmula calibrada de relevância com *cold start* neutro de 3.0 estrelas para congregações sem avaliações (AD-020). |

---

## Rastreabilidade de Requisitos

| ID do Requisito | História / Escopo | Fase | Status |
| --------------- | ------------------ | ---- | ------ |
| SEARCH-01 | P1: Resolução Geográfica e Descoberta Base (Validação de coordenadas, busca territorial e tratamento de erro) | Specify | Confirmado |
| SEARCH-02 | P1: Carga de Preferências do Perfil como Baseline (Personalização teológica e litúrgica automática padrão - AD-021) | Specify | Confirmado |
| SEARCH-03 | P1: Override Efêmero de Filtros e Busca Neutra (Ajustes pontuais na query sem alteração no perfil e busca sem preferências - AD-021) | Specify | Confirmado |
| SEARCH-04 | P1: Ranqueamento Determinístico e Desempate (Ordenação por `matchScore` e cadeia estrita de 4 níveis de desempate - AD-019) | Specify | Confirmado |
| SEARCH-05 | P1: Fórmula Ponderada de Relevância e Atributos Obrigatórios (Pesos 40/45/15, cold start neutro e dados essenciais - AD-020) | Specify | Confirmado |
| SEARCH-06 | P1: Paginação por Cursor e Teto Global de Busca (Keyset pagination com 20 itens/página e encerramento no teto de 100 congregações - AD-022) | Specify | Confirmado |
| SEARCH-07 | P2: Filtragem Avançada e Raio Híbrido (Filtros de idioma/culto e modos Híbrido, Rígido e Suave de `radiusKm` - AD-018) | Specify | Confirmado |
| SEARCH-08 | P2: Origem dos Dados e Deduplicação App-First (Rotulação `app`/`maps` e deduplicação determinística unificada - AD-004 e AD-023) | Specify | Confirmado |

**Cobertura:** 8 requisitos estruturados, 8 confirmados com critérios BDD e decisões arquiteturais vinculadas, 0 pendentes de especificação. Prontos para Design.

---

## Critérios de Sucesso

- [ ] Um usuário consegue concluir o fluxo principal de descoberta em até três ações (informar localização, definir filtros opcionais e visualizar resultados).
- [ ] Todo resultado exibido torna visíveis sua origem (`app` ou `maps`), distância estimada e pontuação de relevância.
- [ ] A ordenação de uma busca com múltiplos resultados é 100% determinística e explicável pela cadeia de desempate confirmada em AD-019.
- [ ] Congregações duplicadas entre a base local e mapas são transparentemente unificadas sob o perfil do app (AD-023), sem duplicação visual de cards ou marcadores.
- [ ] A paginação entrega exatamente até 20 congregações por lote via cursor keyset e encerra de forma instrutiva ao alcançar 100 congregações acumuladas (AD-022).