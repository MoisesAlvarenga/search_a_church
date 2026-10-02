# Especificação de Reivindicação de Perfil de Igreja (Claim)

## Problema

Milhares de igrejas existem no mapa e na base de dados de descoberta sem um gestor oficial vinculado (`Unclaimed`). Para transformar essas entidades em perfis ativos e confiáveis no produto, é necessário permitir que líderes e representantes comprovem sua legitimidade e assumam a gestão do perfil da igreja. Sem um mecanismo seguro de reivindicação, validação de vínculos, trilha de auditoria e resolução de disputas, o sistema fica vulnerável a fraudes, apropriação indevida de perfis comunitários, exposição a riscos jurídicos e desinformação.

## Objetivos

- [ ] Permitir que representantes legítimos iniciem e concluam a reivindicação de igrejas exibidas na plataforma.
- [ ] Implementar ciclo de vida rigoroso com estados `Unclaimed`, `Pending_Verification`, `Verified` e `In_Dispute`.
- [ ] Oferecer métodos de validação estratificados em níveis (**Tier 1** simplificado via presença/social e **Tier 2** pleno via comprovação documental/CNPJ).
- [ ] Definir matriz de permissões segregando o acesso a dados públicos operacionais (Tier 1) de recursos críticos e financeiros (Tier 2).
- [ ] Exigir aceite explícito de Termos de Uso com declaração de veracidade sob penas de falsidade ideológica (art. 299 do Código Penal) e cláusula de isenção de responsabilidade da plataforma.
- [ ] Coletar e armazenar trilha imutável de logs de aplicação em conformidade com o Marco Civil da Internet (Lei nº 12.965/2014).
- [ ] Estabelecer fluxo público de contestação (*Notice and Takedown*) com congelamento imediato e descredenciamento sumário em caso de inércia documental.

## Fora do Escopo

| Funcionalidade | Motivo |
| -------------- | ------ |
| Sistema de processamento de pagamentos ou gateway financeiro | Fora do escopo do produto principal; definido em módulo financeiro futuro. |
| Integração automatizada síncrona com sistemas de cartórios ou juntas comerciais | Validações documentais utilizam cruzamento de dados públicos de CNPJ e análise documental assistida. |
| Gestão interna de membros, escalas pastorais e voluntariado | Pertencem à administração interna da congregação, fora do escopo de perfil e descoberta. |
| Autenticação, recuperação de credenciais e MFA do usuário solicitante | Responsabilidade exclusiva de `authentication-authorization`. |
| Arbitragem judicial de conflitos de propriedade eclesiástica | A plataforma limita-se a regras administrativas de posse do perfil com base em evidências formais registradas. |

---

## Hipóteses e Questões em Aberto

| Hipótese / decisão | Padrão adotado | Justificativa | Confirmada? |
| ------------------ | -------------- | ------------- | ----------- |
| Prioridade probatória em conflitos | Documentos legais registrados (Ata de Posse RCPJ / Estatuto / CNPJ) têm precedência hierárquica absoluta sobre validações digitais ou presença física. | Segurança jurídica, respeito à titularidade legal e prevenção contra engenharia social. | Não |
| Isenção de responsabilidade da plataforma | A plataforma atua como provedora de aplicação (Marco Civil da Internet), isentando-se da veracidade imediata de dados fornecidos via validação simplificada Tier 1. | Mitiga responsabilidade civil da plataforma garantindo mecanismo ágil de remoção e contestação. | Não |
| Retenção de logs do Marco Civil | Coleta obrigatória de IP, porta lógica, timestamp UTC, user-agent e identificadores de verificação, com guarda segura por no mínimo 6 meses. | Cumprimento estrito do art. 15 da Lei Federal nº 12.965/2014. | Não |
| Tempo de expiração de reivindicação pendente | Reivindicações em `Pending_Verification` sem envio de provas expiram em 7 dias corridos. | Libera o perfil para novas tentativas caso o solicitante abandone o fluxo. | Não |
| Prazo de resposta em contestação (`In_Dispute`) | O titular atual do perfil tem 5 dias úteis para responder e enviar contraprovas quando uma disputa formal for aceita. | Garante direito de resposta sem paralisar indefinidamente o perfil. | Não |
| Descredenciamento sumário de Tier 1 | Se o titular atual for verificado exclusivamente por Tier 1 e não apresentar documento legal oficial no prazo de 5 dias úteis após contestação documental com Ata/CNPJ, sofre descredenciamento sumário. | Evita que validadores informais impeçam o legítimo representante legal de assumir o perfil da instituição. | Não |
| Raio de tolerância para geofencing | 100 metros a partir das coordenadas geográficas oficiais cadastradas da igreja. | Compensa margens de erro de GPS móvel em áreas urbanas sem comprometer a comprovação de presença física. | Não |

**Questões em aberto:** nenhuma. Todos os comportamentos críticos estão registrados como hipóteses e critérios de negócio acima.

---

## 1. Ciclo de Vida e Máquina de Estados

### Diagrama de Estados e Transições

```
  ┌────────────────────────────────────────────────────────────────────────┐
  │                                                                        │
  ▼                                                                        │
┌───────────┐      Início de Claim + ToS      ┌──────────────────────┐     │ Expiração / Reprovação Total
│ Unclaimed │ ──────────────────────────────> │ Pending_Verification │ ────┼──────────────────────────────┐
└───────────┘                                 └──────────────────────┘     │                              │
      ▲                                                   │                │                              │
      │                                                   │ Aprovação      │                              │
      │                                                   ▼ de Provas      │                              │
      │                                             ┌───────────┐          │                              │
      │   Fraude mútua /                            │           │ <────────┼──────────┐                   │
      │   Revogação legal                           │ Verified  │          │          │ Contestação       │
      │                                             └───────────┘          │          │ improcedente      │
      │                                                   │                │          │                   │
      │                                                   │ Contestar      │          │                   │
      │                                                   ▼ Propriedade    │          │                   │
      │                                             ┌───────────┐          │          │                   │
      └──────────────────────────────────────────── │In_Dispute │ ─────────┴──────────┘                   │
                                                    └───────────┘                                         │
                                                          │ Descredenciamento Sumário /                   │
                                                          │ Transferência de Titularidade                 │
                                                          └───────────────────────────────────────────────┘
```

### Tabela de Transições de Estado

| Estado Atual | Evento / Condição | Próximo Estado | Efeito no Sistema |
| ------------ | ----------------- | -------------- | ----------------- |
| `Unclaimed` | Solicitante autenticado aceita ToS sob art. 299 CP, registra logs e submete método de validação inicial | `Pending_Verification` | Bloqueia novas reivindicações simples concorrentes; concede acesso preliminar em modo rascunho. |
| `Pending_Verification` | Provas submetidas atingem aprovação exigida (Tier 1 ou Tier 2) | `Verified` | Concede perfil correspondente ao Tier validado; ativa selo da igreja; notifica o representante. |
| `Pending_Verification` | Provas rejeitadas definitivamente (3 tentativas) ou prazo limite de 7 dias expirado | `Unclaimed` | Remove vínculo preliminar; descarta rascunhos não publicados; libera perfil para novos claims. |
| `Verified` | Terceiro aciona "Contestar Propriedade" e anexa documentação legal registrada (Ata/CNPJ) | `In_Dispute` | **Congela imediatamente edições públicas e cadastrais**; notifica titular com prazo de 5 dias úteis. |
| `In_Dispute` | Contestante apresenta documentação legal e titular inicial (Tier 1) permanece inerte ou sem documento legal oficial | `Verified` | **Descredenciamento sumário** do titular inicial e transferência imediata da posse ao contestante legal comprovado. |
| `In_Dispute` | Análise documental julga improcedente a contestação ou titular comprova vigência de mandato legal superior | `Verified` | Mantém a titularidade com o detentor atual e encerra o processo de disputa. |
| `In_Dispute` | Ambas as partes apresentam evidências fraudulentas ou a congregação física foi desativada | `Unclaimed` | Revoga o acesso de ambos e restaura a entidade ao estado neutro não reivindicado. |

---

## 2. Métodos de Validação e Estratificação em Tiers

O sistema categoriza a validação em dois níveis de confiança e segurança probatória:

### Tier 1: Validação Simplificada (Presença Física & Vínculo Digital)
Indicada para agilidade operacional e congregações com liderança local participativa, porém sem acesso imediato a documentos cartorários.
- **Método 1.A - Presença Física (Geofencing + Foto em Tempo Real):**
  - Geofencing móvel validado a no máximo **100 metros** das coordenadas oficiais da igreja.
  - Captura obrigatória de foto em tempo real pela câmera do aplicativo (bloqueado upload de arquivos da galeria).
  - Enquadramento obrigatório da fachada com identificação visual ou interior do templo/púlpito.
- **Método 1.B - Vínculo Digital via Redes Sociais Oficiais:**
  - Geração de token temporário alfanumérico único (`SAC-XXXX-VERIFY`, 24h de validade).
  - Inserção do token na bio/descrição da conta pública da igreja no Instagram, Facebook ou canal do YouTube.
- **Método 1.C - Vínculo Digital via E-mail Institucional:**
  - Envio e confirmação de código OTP de 6 dígitos com validade de 15 minutos em e-mail de domínio próprio correspondente ao site oficial da igreja (ex.: `pastor@igrejabatista.com.br`).

### Tier 2: Validação Plena e Legal (Documental & CNPJ)
Obrigatória para acesso a recursos críticos e prevalente sobre qualquer validação Tier 1 em caso de contestação.
- **Método 2.A - Cruzamento Automático com QSA (Receita Federal):**
  - Solicitante informa CNPJ da entidade religiosa e seu CPF.
  - Cruzamento de dados com a base da Receita Federal: confirmada a posição de Representante Legal / Diretor, concede validação Tier 2 imediata.
- **Método 2.B - Análise de Documento Registrado em Cartório (RCPJ):**
  - Para pastores locais, secretários ou procuradores: upload de Ata de Eleição e Posse vigente registrada em Cartório de Registro Civil de Pessoas Jurídicas (RCPJ), Estatuto Social registrado ou Procuração Pública com poderes específicos para gestão eclesiástica.

---

## 3. Níveis de Permissão por Tipo de Validação (Tiers)

A matriz de permissões segrega de forma rígida o que cada estado e nível de validação pode executar:

| Operação no Perfil da Igreja | `Unclaimed` | `Pending_Verification` (Rascunho) | `Verified` (Tier 1 - Social/GPS) | `Verified` (Tier 2 - Documental/CNPJ) | `In_Dispute` (Congelado) |
| :--- | :---: | :---: | :---: | :---: | :---: |
| Visualizar dados públicos no mapa e lista | Sim | Sim | Sim | Sim | Sim |
| Editar horários de cultos e reuniões | Bloqueado | Permitido (Rascunho) | **Permitido (Publicação)** | **Permitido (Publicação)** | **Bloqueado** |
| Editar fotos, descrição e telefone público | Bloqueado | Permitido (Rascunho) | **Permitido (Publicação)** | **Permitido (Publicação)** | **Bloqueado** |
| Cadastrar/alterar chave PIX ou dados de doação | Bloqueado | Bloqueado | **Bloqueado (Exige Tier 2)** | **Permitido** | **Bloqueado** |
| Convidar e gerenciar outros administradores | Bloqueado | Bloqueado | **Bloqueado (Exige Tier 2)** | **Permitido** | **Bloqueado** |
| Alterar endereço físico ou coordenadas do mapa | Bloqueado | Bloqueado | **Bloqueado (Exige Tier 2)** | **Permitido (com revalidação)** | **Bloqueado** |
| Alterar CNPJ ou Razão Social | Bloqueado | Bloqueado | **Bloqueado (Exige Tier 2)** | **Permitido (com revalidação)** | **Bloqueado** |
| Responder avaliações de visitantes | Bloqueado | Bloqueado | **Permitido** | **Permitido** | **Bloqueado** |
| Selo público exibido na plataforma | Nenhum | *"Em verificação"* | *"Verificação da Comunidade"* | *"Igreja Verificada Oficial"* | *"Em revisão de titularidade"* |
| Transferir titularidade do perfil | Bloqueado | Bloqueado | **Bloqueado** | **Permitido** | **Bloqueado** |

---

## 4. Segurança Jurídica, Termos de Uso e Trilha de Auditoria

### 4.1 Termos de Uso e Declaração de Responsabilidade (ToS)
Para iniciar qualquer solicitação de reivindicação de perfil, o usuário autenticado DEVE obrigatoriamente assinar digitalmente/aceitar os Termos de Uso específicos de reivindicação:
1. **Declaração de Legitimidade sob as Penas da Lei:** O solicitante declara expressamente:
   > *"Declaro, sob as penas da lei e em conformidade com o art. 299 do Código Penal Brasileiro (Falsidade Ideológica), que possuo plenos poderes de representação e legitimidade legal ou eclesiástica para atuar em nome desta congregação religiosa."*
2. **Isenção de Responsabilidade da Plataforma (Safe Harbor):** Os termos estipulam que a plataforma atua como provedora de aplicação nos termos da Lei nº 12.965/2014 (Marco Civil da Internet), não respondendo pela veracidade imediata de dados fornecidos por usuários sob validações simplificadas (Tier 1), reservando-se o direito de congelar, descredenciar e aplicar takedown imediato a qualquer momento mediante notificação fundamentada.
3. **Bloqueio de Continuidade:** O sistema DEVE rejeitar qualquer prosseguimento no fluxo caso o checkbox de aceite da declaração não seja marcado.

### 4.2 Trilha de Auditoria e Coleta Obrigatória de Logs (Marco Civil da Internet)
Em observância ao **art. 15 da Lei Federal nº 12.965/2014 (Marco Civil da Internet)**, para cada evento do ciclo de claim (início, aceite de ToS, submissão de prova, aprovação, contestação e alteração de estado), o sistema DEVE coletar e persistir de forma inviolável os seguintes registros de conexão de aplicação:
- **IP de Origem**: Endereço IPv4 ou IPv6 público utilizado pelo solicitante.
- **Porta Lógica de Origem**: Porta de rede da conexão cliente-servidor.
- **Timestamp Preciso**: Data e hora exatas com fração de segundos em horário UTC, acompanhado do fuso horário local.
- **User-Agent Completo**: String de identificação do navegador, sistema operacional e versão do aplicativo.
- **Identificadores de Verificação Utilizados**:
  - Número de telefone celular validado via OTP (quando aplicável);
  - Link exato e identificador da conta/bio de rede social consultada;
  - Endereço de e-mail institucional validado via token;
  - Hashes dos documentos enviados (Ata/Estatuto) e número de CNPJ/CPF consultados.
- **Políticas de Retenção e Segurança:**
  - Armazenamento em repositório seguro com segregação de acesso e integridade garantida por hash/assinatura digital.
  - Prazo mínimo de guarda: **6 meses**, acessíveis exclusivamente para fins de auditoria interna de segurança ou ordem judicial.

---

## 5. Fluxo de Contestação e Takedown (*Notice and Takedown*)

### 5.1 Gatilho Público de "Contestar Propriedade"
1. Qualquer perfil exibido na plataforma (seja em estado `Verified` Tier 1 ou Tier 2) disponibiliza na interface pública o gatilho: **"Contestar Propriedade desta Igreja"**.
2. O contestante deve estar autenticado e obrigatoriamente fornecer:
   - Identificação completa (Nome, CPF e telefone);
   - Aceite do termo de responsabilidade jurídica sob o art. 299 do Código Penal;
   - Anexação de **prova documental de Tier 2** (Ata de Posse registrada em RCPJ, Estatuto Social ou CNPJ com QSA).
3. **Não serão aceitas contestações baseadas exclusivamente em redes sociais ou geofencing.**

### 5.2 Congelamento Imediato de Dados Públicos (`In_Dispute`)
- No instante da validação da submissão da contestação documental formal:
  - O perfil da congregação transita imediatamente para o estado `In_Dispute`.
  - **Congelamento total de edições:** Fica bloqueada qualquer edição de horários, descrições, fotos, telefones, chaves PIX ou administradores por parte do titular atual ou do contestante.
  - É exibido no perfil público aviso informativo: *"Perfil em processo de verificação de titularidade"*.
  - Notificação formal automática com confirmação de entrega enviada ao atual gestor e ao contestante.

### 5.3 Regra de Descredenciamento Sumário
- O titular atual tem prazo improrrogável de **5 dias úteis** a partir da notificação para apresentar sua defesa e documento legal comprobatório.
- **Cenário de Descredenciamento Sumário:** Se o titular atual possuir apenas credenciamento simplificado (**Tier 1**) e, no prazo de 5 dias úteis:
  - Não responder à notificação; OU
  - Não apresentar documento legal oficial registrado em cartório (Ata de Posse RCPJ ou CNPJ com QSA);
  - **O sistema SHALL executar o descredenciamento sumário imediato do titular atual**, revogando todos os seus acessos administrativos e transferindo a posse definitiva ao contestante documental aprovado, transitando a igreja de `In_Dispute` para `Verified` (Tier 2).

---

## 6. Casos de Borda e Tratamento de Falhas

- **Solicitações Simultâneas / Concorrentes:** O primeiro claim com ToS válido coloca a entidade em `Pending_Verification`. Tentativas concorrentes são bloqueadas, salvo se o segundo requerente apresentar prova documental de Tier 2, caso em que o processo documental ganha precedência e suspende a análise simplificada em andamento.
- **Tentativas Repetidas e Rate Limit:** O solicitante pode realizar até **3 tentativas de reenvio de provas** em 7 dias. Ao atingir o limite de 3 reprovações consecutivas, a solicitação é cancelada, a igreja retorna a `Unclaimed` e o solicitante sofre lockout de **72 horas** para aquela congregação.
- **Inércia em Análise Pendente:** Se uma solicitação em `Pending_Verification` permanecer inativa sem envio de novas evidências por 7 dias corridos, o processo expira automaticamente, descartando rascunhos e liberando o perfil.
- **Fraude Mútua ou Templo Extinto:** Caso a análise de disputa identifique apresentação de atas falsificadas por ambas as partes ou comprove que o templo encerrou atividades no local, a igreja é desvinculada de ambos os usuários e mantida como `Unclaimed` ou desativada.
- **Instabilidade em Serviços Externos de Auditoria:** Se a API de consulta pública de CNPJ ou o serviço de registro de logs sofrer lentidão ou timeout, a requisição transita para fila assíncrona garantida de persistência e NÃO rejeita o usuário sumariamente.

---

## Histórias de Usuário e Critérios de Aceite (BDD)

### P1: Aceite de ToS e Coleta de Logs de Auditoria ⭐ MVP

**História de Usuário**: Como representante legítimo, quero assinar os termos de responsabilidade sob as penas da lei ao reivindicar a igreja para garantir segurança e transparência jurídica no processo.

**Por que P1**: Requisito mandatório de compliance legal, conformidade com o Marco Civil da Internet e proteção civil da plataforma.

#### Cenário 1: Aceite explícito de ToS com registro obrigatório de logs (Marco Civil)
- **GIVEN** que o usuário "Pastor André" está autenticado com IP público "200.180.10.5" e porta "44321"
- **AND** a igreja "Igreja Bíblica Central" está com status `Unclaimed`
- **WHEN** o Pastor André inicia o processo de claim
- **AND** marca a caixa de seleção da declaração: *"Declaro, sob as penas da lei (art. 299 CP), ter poderes de representação..."*
- **AND** clica em "Prosseguir com a Reivindicação"
- **THEN** o sistema SHALL registrar os logs da operação contendo IP, porta lógica, timestamp UTC, user-agent e versão do ToS
- **AND** persistir os dados na trilha de auditoria para guarda legal mínima de 6 meses
- **AND** permitir o avanço para a escolha do método de validação
- **AND** transitar o status da congregação para `Pending_Verification`.

#### Cenário 2: Bloqueio de prosseguimento sem aceite da declaração legal
- **GIVEN** que o solicitante está na tela inicial de reivindicação
- **WHEN** o solicitante tenta submeter o formulário sem marcar o aceite da declaração do art. 299 CP
- **THEN** o sistema SHALL impedir a submissão
- **AND** exibir mensagem de erro destacando que a declaração de legitimidade e os termos são obrigatórios
- **AND** NÃO DEVE alterar o status da igreja para `Pending_Verification`.

---

### P1: Níveis de Permissão por Tipo de Validação (Tier 1 vs Tier 2) ⭐ MVP

**História de Usuário**: Como administrador da plataforma, quero conceder permissões operacionais públicas a líderes validados por métodos simplificados (Tier 1), restringindo recursos financeiros e cadastrais críticos apenas a representantes com validação documental legal (Tier 2).

**Por que P1**: Garante escalabilidade e rapidez para atualização de dados comunitários, mitigando integralmente riscos de fraudes financeiras ou desvios cadastrais.

#### Cenário 3: Acesso restrito a dados públicos após validação Tier 1 (Social/GPS)
- **GIVEN** que o solicitante concluiu a validação da igreja via código na bio do Instagram (Tier 1)
- **WHEN** o sistema conclui a aprovação
- **THEN** o status da igreja SHALL transitar para `Verified` com nível Tier 1
- **AND** o solicitante SHALL poder publicar alterações em horários de cultos, fotos e descrição
- **AND** o selo exibido no perfil público SHALL ser "Verificação da Comunidade".

#### Cenário 4: Bloqueio de chave PIX e alterações críticas para perfil verificado Tier 1
- **GIVEN** que a igreja está no estado `Verified` sob credenciamento Tier 1
- **WHEN** o gestor tenta cadastrar uma chave PIX para arrecadação ou alterar o CNPJ da entidade
- **THEN** o sistema SHALL bloquear a operação
- **AND** exibir mensagem informando que recursos financeiros e alterações cadastrais exigem Validação Documental e CNPJ (Tier 2).

#### Cenário 5: Liberação de recursos críticos após validação Tier 2 (Documental/CNPJ)
- **GIVEN** que o representante submeteu o CNPJ com CPF correspondente no QSA da Receita Federal (Tier 2)
- **WHEN** o sistema confirma a titularidade jurídica
- **THEN** o status da igreja SHALL transitar para `Verified` com nível Tier 2
- **AND** o selo exibido no perfil público SHALL ser "Igreja Verificada Oficial"
- **AND** o sistema SHALL liberar a gestão de chave PIX, convite de novos administradores e atualização de dados cadastrais.

---

### P1: Métodos de Validação Básicos (Presença Física e Vínculo Digital) ⭐ MVP

#### Cenário 6: Reivindicação via Presença Física (Geofencing 100m + Foto em tempo real)
- **GIVEN** que o solicitante está fisicamente no templo da igreja com GPS aferido a 45 metros de distância das coordenadas oficiais
- **WHEN** o solicitante captura foto da fachada em tempo real pela câmera do aplicativo
- **THEN** o sistema SHALL aceitar as coordenadas e armazenar a imagem com carimbo temporal e logs
- **AND** aprovar a validação como Tier 1.

#### Cenário 7: Rejeição de presença física por distância fora do geofence
- **GIVEN** que as coordenadas cadastradas da igreja estão na localização X
- **AND** o solicitante está localizado a 450 metros de distância do templo
- **WHEN** o solicitante tenta capturar a foto de validação presencial
- **THEN** o sistema SHALL bloquear a captura
- **AND** informar que a validação presencial exige estar a menos de 100 metros do local oficial
- **AND** NÃO DEVE alterar o status da igreja.

---

### P2: Fluxo de Contestação Pública e Takedown (Notice and Takedown)

**História de Usuário**: Como representante legal de uma igreja, quero contestar a titularidade de um perfil verificado indevidamente apresentando a Ata de Posse registrada em cartório, para que os dados públicos sejam congelados e a posse seja transferida para mim.

**Por que P2**: Protege a integridade do cadastro comunitário e garante cumprimento rápido de notificações extrajudiciais.

#### Cenário 8: Acionamento de contestação pública com congelamento de edições (In_Dispute)
- **GIVEN** que a igreja está no estado `Verified` sob posse de um usuário com validação Tier 1
- **AND** o pastor presidente legal acessa a página pública da igreja e clica em "Contestar Propriedade"
- **WHEN** o pastor presidente anexa a Ata de Posse registrada em RCPJ e aceita os termos sob art. 299 CP
- **THEN** o sistema SHALL alterar o status da igreja imediatamente para `In_Dispute`
- **AND** congelar qualquer edição nos horários, dados públicos e fotos da igreja
- **AND** notificar o detentor atual com prazo improrrogável de 5 dias úteis para manifestação
- **AND** exibir no perfil o aviso informativo de "Perfil em processo de verificação de titularidade".

#### Cenário 9: Descredenciamento sumário de titular Tier 1 por inércia documental
- **GIVEN** que a igreja está no estado `In_Dispute` com contestação documental legal submetida
- **AND** o titular atual possui apenas verificação Tier 1
- **WHEN** transcorrer o prazo limite de 5 dias úteis sem que o titular atual apresente documento oficial registrado em cartório
- **THEN** o sistema SHALL executar o descredenciamento sumário do titular Tier 1
- **AND** revogar todos os seus acessos administrativos
- **AND** transferir a administração integral ao contestante legal
- **AND** transitar o status da igreja para `Verified` com nível Tier 2
- **AND** registrar o encerramento da disputa na trilha de auditoria.

---

## Dimensões de Requisitos Implícitos (Sweep)

| Dimensão | Cobertura na Especificação |
| -------- | -------------------------- |
| **Validação de Entrada e Limites** | Raio de geofencing de 100m; formato de CNPJ/CPF; fotos em tempo real; validade de OTP (15 min); expiração de token de bio (24h). |
| **Estados de Falha e Parciais** | Retorno a `Unclaimed` por expiração; descarte de rascunhos preliminares; congelamento completo em `In_Dispute`. |
| **Idempotência e Concorrência** | Lock de concorrência atômico ao iniciar claim; desempate com prioridade absoluta para submissões documentais Tier 2. |
| **Fronteiras de Autenticação e Rate Limits** | Requer autenticação prévia; máximo de 3 tentativas por claim; lockout de 72h após 3 falhas consecutivas. |
| **Ciclo de Vida de Dados e Expiração** | Documentos cartorários armazenados com encriptação e segregação; retenção de logs por no mínimo 6 meses (Marco Civil). |
| **Observabilidade e Auditoria** | Coleta obrigatória de IP de origem, porta lógica, timestamp UTC, user-agent e identificadores para todo evento de claim e disputa. |
| **Segurança Jurídica e Compliance** | Declaração sob as penas do art. 299 CP (Falsidade Ideológica); cláusula de isenção de responsabilidade da plataforma (safe harbor). |
| **Integridade de Transição de Estado** | Apenas as 7 transições autorizadas na máquina de estados são aceitas; bloqueio de saltos diretos ilegais. |

---

## Rastreabilidade de Requisitos

| ID do Requisito | História / Área | Fase | Status |
| --------------- | --------------- | ---- | ------ |
| CLAIM-01 | P1: Ciclo de Vida e Estados (Máquina de Estados: Unclaimed, Pending, Verified, In_Dispute) | Specify | Pendente |
| CLAIM-02 | P1: Validação por Presença Física (Geofencing 100m + Foto ao vivo) | Specify | Pendente |
| CLAIM-03 | P1: Validação por Vínculo Digital (Código na Bio de Redes Sociais) | Specify | Pendente |
| CLAIM-04 | P1: Validação por Vínculo Digital (E-mail com Domínio Institucional / OTP) | Specify | Pendente |
| CLAIM-05 | P1: Validação Documental e CNPJ (Cruzamento com QSA e Análise de Ata RCPJ) | Specify | Pendente |
| CLAIM-06 | P1: Termos de Uso, Declaração sob art. 299 CP e Isenção de Responsabilidade | Specify | Pendente |
| CLAIM-07 | P1: Trilha de Auditoria e Logs Obrigatórios conforme Marco Civil da Internet (Lei 12.965/2014) | Specify | Pendente |
| CLAIM-08 | P1: Níveis de Permissão por Tipo de Validação (Tier 1 dados públicos vs Tier 2 recursos críticos/PIX) | Specify | Pendente |
| CLAIM-09 | P2: Tratamento de Concorrência, Rate Limits e Rejeição de Provas | Specify | Pendente |
| CLAIM-10 | P2: Gatilho Público de Contestação de Propriedade e Congelamento em In_Dispute | Specify | Pendente |
| CLAIM-11 | P2: Descredenciamento Sumário de Titular Tier 1 perante Contestação Documental | Specify | Pendente |
| CLAIM-12 | P2: Resolução de Disputa e Transferência Segura de Titularidade | Specify | Pendente |

**Cobertura:** 12 requisitos estruturados, 0 mapeados para tarefas técnicas, 12 aguardando confirmação da especificação.

---

## Critérios de Sucesso

- [ ] 100% dos processos de reivindicação exigem assinatura eletrônica da declaração sob as penas do art. 299 do Código Penal e aceite de isenção da plataforma.
- [ ] 100% dos eventos de claim e disputa geram logs invioláveis com IP, porta lógica, timestamp UTC, user-agent e identificadores (atendendo ao Marco Civil da Internet).
- [ ] Usuários validados exclusivamente por métodos Tier 1 não conseguem cadastrar chaves PIX, alterar dados cadastrais críticos ou convidar administradores.
- [ ] O acionamento de "Contestar Propriedade" congela imediatamente todas as alterações em dados públicos do perfil da congregação.
- [ ] Titulares Tier 1 que não apresentem documento legal oficial no prazo de 5 dias úteis perante contestação documental sofrem descredenciamento sumário automático.
