# Especificação de Avaliações e Feedback

## Problema

Pessoas em busca de igrejas precisam de feedback da comunidade para comparar opções, enquanto colaboradores precisam de uma forma simples de compartilhar experiências. O produto deve suportar feedback confiável sem permitir envios anônimos ou inválidos que distorçam a descoberta.

## Objetivos

- [ ] Usuários autenticados podem enviar avaliação por estrelas e comentário opcional para uma igreja.
- [ ] A busca e os detalhes da igreja podem exibir média e quantidade de avaliações quando houver feedback.
- [ ] Feedback pode ser associado a igrejas cadastradas e resultados de origem do mapa.

## Fora do Escopo

| Funcionalidade | Motivo |
| -------------- | ------ |
| Discussão social geral ou mensagens | Não fazem parte do MVP legado. |
| Moderação automatizada, detecção de abuso ou recursos de apelação | Ainda não há regras de produto definidas. |
| Respostas das igrejas às avaliações | Não descritas no escopo legado. |
| Mecanismo de autenticação | Definido em `authentication-authorization`. |

---

## Hipóteses e Questões em Aberto

| Hipótese / decisão | Padrão adotado | Justificativa | Confirmada? |
| ------------------ | -------------- | ------------- | ----------- |
| Identidade do envio | Apenas usuários autenticados enviam feedback, e o sistema deriva o autor da identidade autenticada. | Avaliações exigem `UserId` no modelo legado e a segurança protege rotas de escrita. | Não |
| Intervalo da avaliação | Avaliações são números inteiros de 1 a 5. | Declarado na especificação legada. | Não |
| Avaliações duplicadas | Um usuário tem uma avaliação ativa por igreja; envios posteriores a atualizam. | Evita inflação de avaliações, embora o legado não defina a regra. | Não |
| Identidade de igreja de origem do mapa | Feedback usa identificador estável do provedor; a mesclagem entre fontes requer confirmação. | Nome e endereço não são identificadores duráveis seguros. | Não |
| Limites e moderação de comentários | Comentários são opcionais; limites, visibilidade e moderação não estão especificados. | A documentação legada não fornece uma política. | Não |

**Questões em aberto:** nenhuma. Todo comportamento não resolvido está registrado como hipótese acima.

---

## Histórias de Usuário

### P1: Enviar uma Avaliação de Igreja MVP

**História de Usuário**: Como usuário autenticado, quero avaliar uma igreja e adicionar comentário opcional para compartilhar feedback com futuras pessoas em busca de igreja.

**Por que P1**: Feedback comunitário é funcionalidade essencial do MVP e entrada para o ranking.

**Critérios de Aceitação**:

1. QUANDO um usuário autenticado enviar uma avaliação de 1 a 5 para uma igreja identificável ENTÃO o sistema DEVE salvar o feedback com esse usuário como autor.
2. QUANDO um usuário autenticado enviar comentário opcional com avaliação válida ENTÃO o sistema DEVE associar o comentário à avaliação.
3. QUANDO um usuário enviar avaliação fora de 1 a 5, sem avaliação ou para igreja não identificável ENTÃO o sistema DEVE rejeitar o envio com resultado de validação claro.
4. QUANDO alguém não autenticado enviar feedback ENTÃO o sistema DEVE negar a solicitação sem criar avaliação.

**Teste Independente**: Enviar avaliação válida, recuperar o feedback da igreja e verificar que envios inválidos ou não autenticados não criam avaliações.

---

### P1: Visualizar Resumo de Feedback MVP

**História de Usuário**: Como pessoa em busca de igreja, quero visualizar avaliações e notas agregadas para incluir a experiência da comunidade na comparação.

**Por que P1**: O fluxo de descoberta deve expor o valor do feedback, não somente coletá-lo.

**Critérios de Aceitação**:

1. QUANDO uma igreja tiver avaliações ENTÃO o sistema DEVE expor sua nota média e quantidade de avaliações.
2. QUANDO um usuário solicitar avaliações de uma igreja identificável ENTÃO o sistema DEVE retornar as avaliações conforme a política de visibilidade confirmada.
3. QUANDO uma igreja não tiver avaliações ENTÃO o sistema DEVE representar o resumo como indisponível ou vazio e NÃO DEVE informar nota inventada.
4. QUANDO existir avaliação de igreja cadastrada ou de origem do mapa ENTÃO o sistema DEVE torná-la elegível ao resumo de feedback da igreja.

**Teste Independente**: Comparar igreja com avaliações, igreja sem avaliações e igreja de origem do mapa com uma avaliação.

## Casos de Borda

- QUANDO um colaborador repetir o envio após tempo esgotado ENTÃO o sistema DEVE evitar avaliações duplicadas conforme a política confirmada.
- QUANDO uma igreja for identificada de forma diferente por `app` e pelo provedor de mapas ENTÃO o sistema NÃO DEVE mesclar feedback até confirmar a identidade.
- QUANDO um comentário falhar em validação ou sanitização ENTÃO o sistema DEVE rejeitá-lo ou neutralizá-lo com segurança antes do armazenamento; a política exata requer confirmação.

## Rastreabilidade de Requisitos

| ID do Requisito | História | Fase | Status |
| --------------- | -------- | ---- | ------ |
| REVIEW-01 | P1: Enviar uma Avaliação de Igreja | Specify | Pendente |
| REVIEW-02 | P1: Enviar uma Avaliação de Igreja | Specify | Pendente |
| REVIEW-03 | P1: Enviar uma Avaliação de Igreja | Specify | Pendente |
| REVIEW-04 | P1: Visualizar Resumo de Feedback | Specify | Pendente |
| REVIEW-05 | P1: Visualizar Resumo de Feedback | Specify | Pendente |

**Cobertura:** 5 no total, 0 mapeados para tarefas, 5 não mapeados aguardando confirmação da especificação.

## Critérios de Sucesso

- [ ] Feedback válido do usuário é refletido corretamente no resumo da igreja correspondente.
- [ ] Uma avaliação não pode ser enviada sem autor autenticado.
- [ ] Pessoas em busca de igreja distinguem ausência de feedback de nota baixa.