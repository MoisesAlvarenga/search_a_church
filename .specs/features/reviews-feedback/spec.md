# Especificação de Avaliações e Feedback

## Problema

Pessoas em busca de igrejas precisam de feedback da comunidade para comparar opções, enquanto colaboradores precisam de uma forma simples de compartilhar experiências. O produto deve suportar feedback confiável sem permitir envios anônimos ou inválidos que distorçam a descoberta, além de proteger a comunidade e as instituições religiosas contra ofensas, palavrões, difamações ou ataques diretos.

## Objetivos

- [ ] Usuários autenticados podem enviar avaliação por estrelas (escala de 1 a 5) e comentário opcional para congregações.
- [ ] Regra de unicidade estrita: cada usuário possui apenas 1 avaliação ativa por igreja, funcionando novos envios como edição/atualização.
- [ ] Filtro automatizado de moderação na submissão de comentários, impedindo insultos, termos vulgares, palavrões ou ataques diretos à instituição e indivíduos.
- [ ] A busca e a página da igreja exibem média aritmética com 1 casa decimal e total de avaliações, integradas ao ranking de relevância.
- [ ] Templos externos do mapa (`origem: maps`) podem ser avaliados via `place_id`, com migração e unificação automática do histórico para o perfil oficial quando este for reivindicado.
- [ ] Mecanismo comunitário de denúncia (*Notice and Takedown*) para ocultação preventiva e moderação técnica de abusos reportados.

## Fora do Escopo

| Funcionalidade | Motivo |
| -------------- | ------ |
| Fórum de discussão aberta ou mensagens privadas | Não fazem parte do produto principal. |
| Respostas públicas da liderança às avaliações | Fase posterior ao MVP. |
| Mecanismo de autenticação e tokens JWT | Definido em `authentication-authorization`. |
| Arbitragem judicial de difamação | A plataforma adota moderação administrativa e cooperação nos termos do Marco Civil da Internet. |

---

## Hipóteses e Questões em Aberto

| Hipótese / decisão | Padrão adotado | Justificativa | Confirmada? |
| ------------------ | -------------- | ------------- | ----------- |
| Identidade do envio | Escrita protegida: apenas usuários autenticados via JWT enviam avaliações; o backend deriva a autoria do claim `sub`, vedando envios anônimos. | Confirmado em AD-027: integridade da prova social e prevenção contra abusos em massa. | Sim |
| Intervalo da avaliação | Nota obrigatória em números inteiros de 1 a 5 estrelas; backend calcula e expõe média aritmética com 1 decimal e contagem total (`review_count`). | Confirmado em AD-019, AD-020 e AD-027: padrão universal intuitivo integrado à pontuação de relevância. | Sim |
| Avaliações duplicadas | Unicidade estrita: 1 avaliação ativa por usuário por igreja. Reenvios são processados como atualização (*upsert*), recalculando a média sem inflar a contagem. | Confirmado em AD-027: previne distorção artificial de notas e contagens. | Sim |
| Identidade de igreja de origem do mapa | Templos externos do mapa podem ser avaliados atrelando o feedback ao `place_id`. Ao haver reivindicação ou cadastro oficial, as avaliações são unificadas no perfil oficial sem perdas. | Confirmado em AD-004, AD-023 e AD-027: preserva o histórico comunitário gerado antes da oficialização do perfil. | Sim |
| Limites de texto e moderação de vocabulário | Comentário opcional de até 500 caracteres com sanitização anti-XSS e filtro automatizado de vocabulário: rejeição imediata na submissão de palavras ofensivas, palavrões, insultos ou ataques à instituição. | Confirmado em AD-027: mantém ambiente respeitoso e blindagem jurídica contra abusos verbais. | Sim |
| Mecanismo de denúncia comunitária | Disponibilização de ação "Denunciar avaliação"; suspensão preventiva temporária do comentário textual sob 3 denúncias de usuários distintos ou contestação do representante verificado da congregação. | Confirmado em AD-013 e AD-027: conformidade com o princípio de Notice and Takedown do Marco Civil da Internet. | Sim |

**Questões em aberto:** nenhuma. Todas as hipóteses de avaliações e feedback estão integralmente confirmadas pelo responsável do produto.

---

## Histórias de Usuário

### P1: Enviar e Editar Avaliação de Igreja MVP

**História de Usuário**: Como usuário autenticado, quero avaliar uma igreja com nota de 1 a 5 estrelas e comentário opcional respeitoso, para compartilhar minha experiência e ajudar outros viajantes e pessoas em busca de igreja.

**Por que P1**: Prova social essencial do produto e entrada para a pontuação de relevância da busca.

**Critérios de Aceitação**:

1. QUANDO um usuário autenticado enviar uma avaliação com nota inteira de 1 a 5 ENTÃO o sistema DEVE salvar o feedback associado ao seu `user_id` e à igreja selecionada.
2. QUANDO um usuário autenticado enviar comentário opcional (de até 500 caracteres) ENTÃO o sistema DEVE associar o comentário à sua avaliação.
3. QUANDO o comentário submetido contiver termos ofensivos, insultos, palavras de baixo calão, palavrões ou ataques diretos à instituição ENTÃO o sistema DEVE rejeitar a submissão com erro HTTP 422 `CONTEUDO_INADEQUADO`, informando que o texto viola as diretrizes de respeito da comunidade.
4. QUANDO um usuário não autenticado tentar enviar avaliação ENTÃO o sistema DEVE negar a requisição com erro HTTP 401 `NAO_AUTENTICADO`.
5. QUANDO um usuário enviar nova avaliação para uma igreja que ele já avaliou anteriormente ENTÃO o sistema DEVE atualizar o registro existente (upsert), atualizar a data de modificação e recalcular a nota média da igreja, SEM incrementar a contagem total de avaliações (`review_count`).
6. QUANDO um usuário solicitar a exclusão de sua própria avaliação ENTÃO o sistema DEVE remover o registro e recalcular a média e a contagem da congregação.

**Teste Independente**: Enviar avaliação válida; enviar comentário com palavrão simulado e verificar rejeição por validação; reenviar avaliação com nova nota e comprovar a edição sem duplicação de contagem.

---

### P1: Visualizar Resumo Agregado de Feedback MVP

**História de Usuário**: Como pessoa em busca de igreja, quero visualizar a nota média e a quantidade total de avaliações de cada congregação na busca e nos detalhes, para comparar a reputação comunitária das opções.

**Por que P1**: Valoriza a experiência comunitária na tomada de decisão de visitação do usuário.

**Critérios de Aceitação**:

1. QUANDO uma congregação possuir avaliações ENTÃO o sistema DEVE expor sua nota média com 1 casa decimal (ex.: `4.7`) e a contagem total de avaliações (`review_count`).
2. QUANDO uma congregação não possuir avaliações ENTÃO o sistema DEVE representar a nota como nula/não avaliada e NÃO DEVE exibir notas fictícias na interface.
3. QUANDO uma congregação recém-cadastrada participar do ranqueamento da busca ENTÃO o sistema DEVE aplicar a baseline neutra de cold-start (3.0 estrelas nos 15% de reputação sob AD-020) sem exibir nota pública inventada.

**Teste Independente**: Comparar igreja avaliada e igreja nova sem avaliações e checar valores médios e contadores retornados.

---

### P2: Avaliar Templos Externos do Mapa e Migração Pós-Claim

**História de Usuário**: Como frequentador de uma igreja localizada no mapa que ainda não possui perfil oficial no aplicativo, quero avaliá-la para que meu feedback já beneficie a comunidade e seja preservado quando a igreja for reivindicada.

**Por que P2**: Amplia a densidade de prova social mesmo em fases iniciais de adesão de líderes à plataforma.

**Critérios de Aceitação**:

1. QUANDO um usuário autenticado avaliar uma igreja descoberta no mapa (`origem: maps`) ENTÃO o sistema DEVE persistir a avaliação atrelada ao `place_id` da entidade externa.
2. QUANDO a igreja vinculada ao `place_id` for posteriormente cadastrada ou homologada via `church-profile-claim` ENTÃO o sistema DEVE migrar e unificar automaticamente todas as avaliações existentes para o identificador oficial da congregação (`origem: app`), preservando notas, comentários e contadores sem duplicidades.

**Teste Independente**: Avaliar igreja de mapa via `place_id`, simular homologação de perfil e comprovar que as avaliações foram transferidas para a entidade oficial.

---

### P2: Mecanismo de Denúncia e Ocultação Preventiva de Comentários

**História de Usuário**: Como membro da comunidade ou representante de igreja, quero denunciar comentários abusivos ou difamatórios para que a moderação técnica revise e remova conteúdo prejudicial.

**Por que P2**: Segurança jurídica e cumprimento de boas práticas do Marco Civil da Internet (*Notice and Takedown*).

**Critérios de Aceitação**:

1. QUANDO um usuário autenticado acionar "Denunciar avaliação" informando o motivo ENTÃO o sistema DEVE registrar o incidente de moderação com trilha de auditoria.
2. QUANDO uma avaliação acumular 3 ou mais denúncias de usuários distintos ou for formalmente contestada pelo representante verificado da congregação ENTÃO o sistema DEVE suspender temporariamente a visibilidade pública do comentário textual (*status: Under_Review*), mantendo o cômputo da nota até julgamento pela moderação técnica.

**Teste Independente**: Simular o acúmulo de denúncias para um comentário e comprovar sua ocultação preventiva da consulta pública.

---

## Casos de Borda

- QUANDO o comentário textual contiver tentativas de injeção de tags HTML ou scripts maliciosos ENTÃO o sistema DEVE sanitizar e neutralizar o texto antes do armazenamento.
- QUANDO o comentário contiver variações propositais de grafia ou caracteres especiais para burlar o filtro de palavrões ENTÃO o sistema DEVE aplicar normalização léxica (leetspeak/acentos) para detecção eficaz de termos bloqueados.
- QUANDO um usuário tentar avaliar uma congregação inexistente ENTÃO o sistema DEVE rejeitar com erro HTTP 404 `CONGREGACAO_NAO_ENCONTRADA`.
- QUANDO a conta de um usuário for encerrada via soft delete (LGPD) ENTÃO suas avaliações anteriores DEVEM ter os dados pessoais do autor anonimizados, preservando nota e comentário se não houver contestação.

---

## Rastreabilidade de Requisitos

| ID do Requisito | História / Área | Fase | Status |
| --------------- | --------------- | ---- | ------ |
| REVIEW-01 | P1: Enviar Avaliação por Estrelas (1 a 5) com Autoria Autenticada via JWT (AD-027) | Specify | Confirmado |
| REVIEW-02 | P1: Unicidade de Avaliação e Atualização Contínua por Usuário (Upsert - AD-027) | Specify | Confirmado |
| REVIEW-03 | P1: Filtro Automatizado de Insultos, Palavrões e Ataques à Instituição (AD-027) | Specify | Confirmado |
| REVIEW-04 | P1: Visualizar Resumo Agregado de Feedback (Média com 1 Decimal e Contagem - AD-019, AD-020 e AD-027) | Specify | Confirmado |
| REVIEW-05 | P2: Avaliação de Templos Externos do Mapa via `place_id` e Migração Pós-Claim (AD-004 e AD-027) | Specify | Confirmado |
| REVIEW-06 | P2: Mecanismo de Denúncia e Ocultação Preventiva de Comentários (Notice and Takedown - AD-013 e AD-027) | Specify | Confirmado |

**Cobertura:** 6 requisitos estruturados, 6 confirmados com critérios BDD e decisões arquiteturais vinculadas, 0 pendentes de especificação. Prontos para Design.

---

## Critérios de Sucesso

- [ ] Avaliações refletem feedback real de usuários autenticados sem envios anônimos ou duplicados.
- [ ] O filtro de moderação bloqueia proativamente termos ofensivos, insultos, palavrões e difamações.
- [ ] Médias e volumes de avaliações são calculados com precisão e integram-se ao ranking de relevância.
- [ ] Feedbacks de igrejas descobertas no mapa são preservados e transferidos na oficialização do perfil.
- [ ] Mecanismo comunitário de denúncia permite remoção célere de conteúdo que fira as diretrizes da plataforma.