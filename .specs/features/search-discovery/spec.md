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
| Significado de `radiusKm` | É uma entrada de ranqueamento, não um filtro de exclusão. | O MVP e a especificação de ranking dizem que a distância não deve excluir resultados por si só; um documento de teste legado diverge. | Não |
| Ranqueamento padrão | `matchScore` decrescente, com ordenação secundária determinística a definir em Design. | A especificação legada define pontuação decrescente, mas não define empates. | Não |
| Fórmula de pontuação | O produto exige ranking comparável considerando compatibilidade de perfil, distância, idioma, tipo de culto e feedback; pesos e normalização ainda não foram aprovados pelo produto. | O código contém pesos, mas a documentação de produto não os valida. | Não |
| Buscas sem perfil salvo | O usuário pode buscar por localização e preferências diretas opcionais; os resultados permanecem ranqueados com as entradas disponíveis. | A API legada divulga ID de perfil e filtros diretos, mas não define sua precedência. | Não |
| Limites de resultados | A especificação inicial não exige paginação nem quantidade máxima de resultados. | Nenhum limite está documentado; exige confirmação antes de uma implementação escalável. | Não |

**Questões em aberto:** nenhuma. Todo comportamento legado não resolvido está registrado como hipótese acima.

---

## Histórias de Usuário

### P1: Encontrar Igrejas por Localização e Preferências MVP

**História de Usuário**: Como viajante ou pessoa em mudança, quero buscar a partir de uma localização com minhas preferências disponíveis para encontrar igrejas relevantes para minha situação.

**Por que P1**: Esta é a proposta de valor central do MVP.

**Critérios de Aceitação**:

1. QUANDO um usuário informar uma localização válida para busca ENTÃO o sistema DEVE retornar resultados de igrejas associados àquela localização.
2. QUANDO um usuário informar um perfil salvo ou preferências diretas ENTÃO o sistema DEVE usar as preferências disponíveis para determinar a relevância de cada resultado.
3. QUANDO um usuário não informar preferências ENTÃO o sistema DEVE ainda retornar resultados que considerem a localização, em vez de rejeitar a busca apenas pela ausência de perfil.
4. QUANDO uma localização não puder ser resolvida para uma área geográfica de busca ENTÃO o sistema DEVE informar que a localização é inválida ou não foi resolvida e NÃO DEVE apresentá-la como busca bem-sucedida.

**Teste Independente**: Buscar a partir de uma localização conhecida com e sem preferências e verificar que os resultados são retornados ou que a falha de localização é explícita.

---

### P1: Comparar Resultados Ranqueados MVP

**História de Usuário**: Como pessoa em busca de igreja, quero resultados ordenados por relevância e explicados com informações essenciais para comparar igrejas rapidamente.

**Por que P1**: Uma lista de igrejas sem ranqueamento compreensível não resolve o problema de descoberta.

**Critérios de Aceitação**:

1. QUANDO uma busca retornar mais de um resultado ENTÃO o sistema DEVE apresentar os resultados em ordem decrescente de relevância.
2. QUANDO o sistema avaliar relevância ENTÃO DEVE considerar compatibilidade de perfil disponível, distância, idioma, tipo de culto e informações de feedback.
3. QUANDO um resultado for exibido ENTÃO DEVE incluir nome, endereço, posição geográfica, distância estimada, horário de culto quando conhecido, atributos de perfil quando conhecidos, tipo de culto quando conhecido, pontuação de relevância, resumo de avaliação quando conhecido, status de cadastro e origem.
4. QUANDO dois resultados forem provenientes de fontes diferentes ENTÃO o sistema DEVE avaliá-los sob a mesma política de ranqueamento por relevância.

**Teste Independente**: Buscar uma localização com igrejas de compatibilidade diferente e verificar a ordenação exibida e os campos de cada resultado.

---

### P2: Filtrar uma Busca de Descoberta

**História de Usuário**: Como pessoa em busca de igreja, quero restringir resultados por idioma, tipo de culto e distância desejada para que o ranqueamento reflita restrições práticas.

**Por que P2**: Filtros melhoram a relevância, mas não são necessários para demonstrar o fluxo básico de busca.

**Critérios de Aceitação**:

1. QUANDO um usuário informar uma preferência de idioma ENTÃO o sistema DEVE aplicá-la como entrada para avaliação de relevância.
2. QUANDO um usuário informar uma preferência de tipo de culto ENTÃO o sistema DEVE aplicá-la como entrada para avaliação de relevância.
3. QUANDO um usuário informar uma distância desejada ENTÃO o sistema DEVE usar a distância no ranqueamento e DEVE continuar exibindo resultados fora dessa distância, salvo se um requisito futuro confirmado alterar essa política.
4. QUANDO um valor de filtro estiver malformado ou fora dos limites aceitos ENTÃO o sistema DEVE rejeitá-lo com um resultado de validação claro.

**Teste Independente**: Executar buscas equivalentes com cada filtro válido e com um filtro malformado, depois comparar o ranqueamento ou o resultado de validação.

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
- QUANDO registros duplicados representarem a mesma igreja em fontes diferentes ENTÃO o sistema DEVE aplicar uma política de deduplicação antes de apresentar os resultados finais; a política requer confirmação.
- QUANDO dados de feedback não estiverem disponíveis ENTÃO o sistema DEVE preservar o resultado e representar o resumo de avaliação como indisponível.

## Rastreabilidade de Requisitos

| ID do Requisito | História | Fase | Status |
| --------------- | -------- | ---- | ------ |
| SEARCH-01 | P1: Encontrar Igrejas por Localização e Preferências | Specify | Pendente |
| SEARCH-02 | P1: Encontrar Igrejas por Localização e Preferências | Specify | Pendente |
| SEARCH-03 | P1: Encontrar Igrejas por Localização e Preferências | Specify | Pendente |
| SEARCH-04 | P1: Comparar Resultados Ranqueados | Specify | Pendente |
| SEARCH-05 | P1: Comparar Resultados Ranqueados | Specify | Pendente |
| SEARCH-06 | P1: Comparar Resultados Ranqueados | Specify | Pendente |
| SEARCH-07 | P2: Filtrar uma Busca de Descoberta | Specify | Pendente |
| SEARCH-08 | P2: Identificar a Origem dos Dados | Specify | Pendente |

**Cobertura:** 8 no total, 0 mapeados para tarefas, 8 não mapeados aguardando confirmação da especificação.

## Critérios de Sucesso

- [ ] Um usuário consegue concluir o fluxo principal de descoberta em até três ações.
- [ ] Todo resultado exibido torna visíveis sua origem e distância estimada.
- [ ] A ordenação de uma busca com múltiplos resultados é explicável pela política de relevância confirmada.