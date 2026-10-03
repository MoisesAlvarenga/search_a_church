# Especificação de Reivindicação de Perfil de Igreja (Claim)

## Problema

Milhares de igrejas existem no mapa e na base de dados de descoberta sem um gestor oficial vinculado (`Unclaimed`). Para transformar essas entidades em perfis ativos e confiáveis no produto, é necessário permitir que líderes e representantes comprovem sua legitimidade e assumam a gestão do perfil da igreja. Sem um mecanismo seguro de reivindicação, validação de vínculos, trilha de auditoria e resolução de disputas, o sistema fica vulnerável a fraudes, apropriação indevida de perfis comunitários, exposição a riscos jurídicos e desinformação.

## Objetivos

- [ ] Permitir que representantes legítimos iniciem e concluam a reivindicação de igrejas exibidas na plataforma.
- [ ] Implementar ciclo de vida rigoroso com estados `Unclaimed`, `Pending_Verification`, `Verified` e `In_Dispute`.
- [ ] Adotar **Hierarquia Probatória Estrita em 3 Níveis** (Nível 1: Cartório RCPJ/CNPJ; Nível 2: Domínio/Canais Institucionais; Nível 3: Sociais/Presenciais).
- [ ] Definir matriz de permissões segregando o acesso a dados operacionais básicos (Nível 3 e 2) de recursos críticos e financeiros/PIX (exclusivo Nível 1).
- [ ] Exigir aceite explícito de Termos de Uso com declaração de veracidade sob penas de falsidade ideológica (art. 299 do Código Penal) e cláusula de isenção de responsabilidade da plataforma.
- [ ] Coletar e armazenar trilha imutável de logs de aplicação em conformidade com o Marco Civil da Internet (Lei nº 12.965/2014).
- [ ] Estabelecer a **Regra de Resolução Automática de Disputa**: documentos de Nível 1 sobrepõem e revogam sumariamente vínculos obtidos via Nível 2 ou 3, notificando o detentor anterior por prevalência legal, sem direito a bloqueio unilateral.
- [ ] Prever rito paritário de contestação com congelamento imediato (`In_Dispute`) quando houver conflito entre evidências de mesma hierarquia (Nível 1 vs Nível 1).

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
| Prioridade probatória em conflitos | **Hierarquia Probatória Estrita em 3 níveis**: Nível 1 (Documentos públicos registrados em Cartório - RCPJ / Estatuto / CNPJ-QSA), Nível 2 (Canais institucionais proprietários - domínio próprio verificado / e-mail do domínio) e Nível 3 (Validações sociais e presenciais - Instagram, Facebook, GPS, fotos). **Resolução Automática de Disputa**: qualquer contestação com documentação válida de Nível 1 sobrepõe e revoga sumariamente vínculos obtidos via Nível 2 ou 3. O detentor anterior é notificado da revogação por prevalência documental legal, sem direito a bloqueio unilateral do processo. | Segurança jurídica, respeito à titularidade legal e prevenção contra engenharia social ou bloqueios infundados. | **Sim** |
| Isenção de responsabilidade da plataforma | A plataforma atua como provedora de aplicação (Marco Civil da Internet), isentando-se da veracidade imediata de dados fornecidos via validação simplificada Nível 2 ou Nível 3. | Mitiga responsabilidade civil da plataforma garantindo mecanismo ágil de remoção e contestação. | Não |
| Retenção de logs do Marco Civil | Coleta obrigatória de IP, porta lógica, timestamp UTC, user-agent e identificadores de verificação, com guarda segura por no mínimo 6 meses. | Cumprimento estrito do art. 15 da Lei Federal nº 12.965/2014. | Não |
| Tempo de expiração de reivindicação pendente | Reivindicações em `Pending_Verification` sem envio de provas expiram em 7 dias corridos. | Libera o perfil para novas tentativas caso o solicitante abandone o fluxo. | Não |
| Prazo de resposta em contestação paritária (`In_Dispute`) | As partes em disputa Nível 1 vs Nível 1 têm 5 dias úteis para apresentar certidões atualizadas de vigência de mandato cartorial. | Garante contraditório formal sem paralisar indefinidamente o perfil da instituição. | Não |
| Revogação sumária de Nível 2 e Nível 3 | Contestação válida com documento de Nível 1 revoga sumariamente e imediatamente os acessos de titulares de Nível 2 ou 3, sem exigência de aguardar inércia ou prazo de defesa prévia. | Prevalência incontestável da fé pública e registros públicos cartoriais. | **Sim** |
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
| `Pending_Verification` | Provas submetidas atingem aprovação exigida (Nível 1, Nível 2 ou Nível 3) | `Verified` | Concede perfil correspondente ao nível validado; ativa selo da igreja; notifica o representante. |
| `Pending_Verification` | Provas rejeitadas definitivamente (3 tentativas) ou prazo limite de 7 dias expirado | `Unclaimed` | Remove vínculo preliminar; descarta rascunhos não publicados; libera perfil para novos claims. |
| `Verified` (Nível 2 ou 3) | Requerente submete contestação acompanhada de documentação válida de Nível 1 | `Verified` (Nível 1) | **Resolução Automática de Disputa:** revoga sumariamente o vínculo anterior de Nível 2 ou 3 e transfere posse ao requerente de Nível 1; notifica titular anterior por prevalência documental legal, sem direito a bloqueio unilateral. |
| `Verified` (Nível 1) | Terceiro apresenta contestação documental também de Nível 1 (mandatos/atas concorrentes) | `In_Dispute` | **Congela imediatamente edições públicas, cadastrais e financeiras**; notifica ambas as partes para análise de vigência da ata em 5 dias úteis. |
| `In_Dispute` | Análise administrativa documental confirma ata de eleição mais recente e mandato vigente | `Verified` (Nível 1) | Restaura poderes e confirma titularidade definitiva ao representante legítimo; encerra disputa. |
| `In_Dispute` | Ambas as partes apresentam evidências fraudulentas ou a congregação física foi desativada | `Unclaimed` | Revoga o acesso de ambos e restaura a entidade ao estado neutro não reivindicado. |

---

## 2. Métodos de Validação e Hierarquia Probatória Estrita

O sistema categoriza a validação em **três níveis rigorosamente hierárquicos** de autoridade probatória:

### Nível 1: Documentos Públicos Registrados em Cartório (RCPJ) & CNPJ/QSA (Máxima Autoridade)
Possui autoridade jurídica máxima e incontestável na plataforma. Em caso de conflito, **sobrepõe e revoga sumariamente qualquer vínculo obtido via Nível 2 ou Nível 3**. Exigido obrigatoriamente para acesso a recursos financeiros/PIX, alterações cadastrais críticas e transferência de titularidade.
- **Método 1.A - Documentos Cartorários (RCPJ):**
  - Upload e conferência de Ata de Posse da Diretoria vigente registrada em Cartório de Registro Civil de Pessoas Jurídicas (RCPJ).
  - Estatuto Social registrado com poderes expressos da diretoria ou Procuração Pública com poderes específicos de gestão e representação eclesiástica.
- **Método 1.B - Cruzamento Automático com QSA (Receita Federal):**
  - Solicitante informa CNPJ da entidade religiosa e seu CPF.
  - Cruzamento de dados com a base pública da Receita Federal: confirmada a qualificação como Representante Legal / Presidente no Quadro de Sócios e Administradores (QSA), concede validação de Nível 1 imediata.

### Nível 2: Canais Institucionais Proprietários
Indicado para organizações que possuem infraestrutura digital formal e controlada diretamente pela instituição religiosa.
- **Método 2.A - Domínio Próprio Verificado:**
  - Verificação de controle técnico do domínio oficial da igreja por meio de inserção de registro DNS (entrada TXT ou CNAME com token de validação) apontando para a aplicação.
- **Método 2.B - E-mail de Domínio Institucional:**
  - Envio e confirmação de código OTP de 6 dígitos com validade de 15 minutos em endereço de e-mail institucional próprio da congregação (ex.: `pastor@igrejabatista.com.br` ou `secretaria@adcentral.org.br`). Bloqueados provedores de webmail genéricos e gratuitos (ex.: `@gmail.com`, `@hotmail.com`, `@outlook.com`).

### Nível 3: Validações Sociais e Presenciais
Indicado para agilidade comunitária e congregações com liderança local participativa, porém sem acesso imediato a documentos cartorários ou domínio proprietário.
- **Método 3.A - Presença Física (Geofencing + Foto em Tempo Real):**
  - Geofencing móvel validado a no máximo **100 metros** das coordenadas oficiais da igreja.
  - Captura obrigatória de foto em tempo real pela câmera do aplicativo (bloqueado upload de arquivos da galeria).
  - Enquadramento obrigatório da fachada com identificação visual ou interior do templo/púlpito.
- **Método 3.B - Vínculo em Redes Sociais Oficiais:**
  - Geração de token temporário alfanumérico único (`SAC-XXXX-VERIFY`, 24h de validade).
  - Inserção do token na bio/descrição da conta pública da igreja no Instagram, Facebook ou canal oficial no YouTube.

---

## 3. Níveis de Permissão por Hierarquia Probatória

A matriz de permissões segrega de forma rígida o que cada estado e nível de validação pode executar:

| Operação no Perfil da Igreja | `Unclaimed` | `Pending_Verification` (Rascunho) | `Verified` (Nível 3 - Social/GPS) | `Verified` (Nível 2 - Domínio/E-mail) | `Verified` (Nível 1 - Cartório/CNPJ) | `In_Dispute` (Congelado) |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| Visualizar dados públicos no mapa e lista | Sim | Sim | Sim | Sim | Sim | Sim |
| Editar horários de cultos e reuniões | Bloqueado | Permitido (Rascunho) | **Permitido (Publicação)** | **Permitido (Publicação)** | **Permitido (Publicação)** | **Bloqueado** |
| Editar fotos, descrição e telefone público | Bloqueado | Permitido (Rascunho) | **Permitido (Publicação)** | **Permitido (Publicação)** | **Permitido (Publicação)** | **Bloqueado** |
| Cadastrar/alterar chave PIX ou dados de doação | Bloqueado | Bloqueado | **Bloqueado (Exige Nível 1)** | **Bloqueado (Exige Nível 1)** | **Permitido** | **Bloqueado** |
| Convidar e gerenciar outros administradores | Bloqueado | Bloqueado | **Bloqueado (Exige Nível 1)** | **Permitido (Operacional)** | **Permitido (Total)** | **Bloqueado** |
| Alterar endereço físico ou coordenadas do mapa | Bloqueado | Bloqueado | **Bloqueado (Exige Nível 1)** | **Permitido (Revalidação)** | **Permitido (Revalidação)** | **Bloqueado** |
| Alterar CNPJ ou Razão Social | Bloqueado | Bloqueado | **Bloqueado (Exige Nível 1)** | **Bloqueado (Exige Nível 1)** | **Permitido (Revalidação)** | **Bloqueado** |
| Responder avaliações de visitantes | Bloqueado | Bloqueado | **Permitido** | **Permitido** | **Permitido** | **Bloqueado** |
| Selo público exibido na plataforma | Nenhum | *"Em verificação"* | *"Verificação da Comunidade"* | *"Verificação Institucional"* | *"Igreja Verificada Oficial"* | *"Em revisão de titularidade"* |
| Transferir titularidade do perfil | Bloqueado | Bloqueado | **Bloqueado** | **Bloqueado** | **Permitido** | **Bloqueado** |

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

## 5. Fluxo de Contestação, Takedown e Resolução de Disputas

### 5.1 Regra de Resolução Automática de Disputa (Prevalência Documental Legal)
1. **Hierarquia Probatória Estrita:**
   - **Nível 1 (Máxima Autoridade):** Documentos públicos registrados em Cartório (RCPJ) — Ata de Posse da Diretoria, Estatuto Social e Cartão CNPJ/QSA.
   - **Nível 2:** Canais institucionais proprietários (domínio próprio verificado / e-mail do domínio).
   - **Nível 3:** Validações sociais e presenciais (Instagram, Facebook, GPS, fotos).
2. **Resolução Automática:**
   - Qualquer contestação acompanhada de documentação válida de **Nível 1 sobrepõe e revoga sumariamente** vínculos obtidos via Nível 2 ou Nível 3.
   - O detentor anterior (Nível 2 ou 3) é **notificado da revogação por prevalência documental legal**, sem direito a bloqueio unilateral do processo.
   - A posse é transferida de imediato ao titular comprovado de Nível 1 sob o estado `Verified`, mantendo a integridade cadastral e eliminando manobras de obstrução por titulares de níveis inferiores.

### 5.2 Gatilho Público de "Contestar Propriedade"
1. Qualquer perfil exibido na plataforma (esteja em estado `Verified` Nível 1, 2 ou 3) disponibiliza na interface pública o gatilho: **"Contestar Propriedade desta Igreja"**.
2. O contestante deve estar autenticado e obrigatoriamente fornecer:
   - Identificação completa (Nome, CPF e telefone);
   - Aceite do termo de responsabilidade jurídica sob as penas do art. 299 do Código Penal;
   - Anexação de **prova documental válida de Nível 1** (Ata de Posse registrada em RCPJ, Estatuto Social ou CNPJ com QSA).
3. **Não serão aceitas contestações fundamentadas exclusivamente em evidências de Nível 2 ou Nível 3 contra perfis verificados.**

### 5.3 Conflito Paritário de Mesma Hierarquia (Nível 1 vs Nível 1) e Congelamento (`In_Dispute`)
- Quando a contestação documental de Nível 1 for instaurada contra um perfil que **já possuía credenciamento de Nível 1** (conflito entre diretorias ou atas concorrentes):
  - O perfil da congregação transita imediatamente para o estado `In_Dispute`.
  - **Congelamento total de edições:** Fica bloqueada qualquer edição de horários, descrições, fotos, telefones, chaves PIX ou administradores por ambas as partes.
  - É exibido no perfil público aviso informativo: *"Perfil em processo de verificação de titularidade"*.
  - Notificação formal automática enviada a ambas as partes com prazo improrrogável de **5 dias úteis** para apresentação de certidão atualizada de breve relato do RCPJ comprovando a vigência e tempestividade do mandato da diretoria.
  - A administração da plataforma valida a ata de eleição mais recente registrada em cartório para confirmar a posse definitiva.

---

## 6. Casos de Borda e Tratamento de Falhas

- **Solicitações Simultâneas / Concorrentes:** O primeiro claim com ToS válido coloca a entidade em `Pending_Verification`. Tentativas concorrentes são bloqueadas, salvo se o segundo requerente apresentar prova de nível hierárquico superior (ex.: Nível 1 contra processo Nível 2 ou 3), caso em que a submissão de maior autoridade ganha precedência imediata e cancela o processo concorrente inferior.
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

### P1: Níveis de Permissão por Hierarquia Probatória (Níveis 1, 2 e 3) ⭐ MVP

**História de Usuário**: Como administrador da plataforma, quero conceder permissões operacionais públicas a líderes validados por canais institucionais (Nível 2) ou comunitários (Nível 3), restringindo recursos financeiros e cadastrais críticos exclusivamente a representantes com validação documental cartorial e CNPJ (Nível 1).

**Por que P1**: Garante escalabilidade e rapidez para atualização de dados comunitários, mitigando integralmente riscos de fraudes financeiras ou desvios cadastrais.

#### Cenário 3: Acesso a dados operacionais após validação Nível 3 (Social/GPS)
- **GIVEN** que o solicitante concluiu a validação da igreja via código na bio do Instagram ou foto presencial via geofencing (Nível 3)
- **WHEN** o sistema conclui a aprovação
- **THEN** o status da igreja SHALL transitar para `Verified` com nível Nível 3
- **AND** o solicitante SHALL poder publicar alterações em horários de cultos, fotos e descrição
- **AND** o selo exibido no perfil público SHALL ser "Verificação da Comunidade".

#### Cenário 4: Bloqueio de chave PIX e alterações críticas para perfis de Nível 3 ou Nível 2
- **GIVEN** que a igreja está no estado `Verified` sob credenciamento de Nível 3 (Social/GPS) ou Nível 2 (E-mail institucional)
- **WHEN** o gestor tenta cadastrar uma chave PIX para arrecadação ou alterar o CNPJ da entidade
- **THEN** o sistema SHALL bloquear a operação
- **AND** exibir mensagem informando que recursos financeiros e alterações cadastrais exigem Validação Documental Cartorial e CNPJ (Nível 1).

#### Cenário 5: Liberação de recursos críticos após validação Nível 1 (Cartório/CNPJ)
- **GIVEN** que o representante submeteu a Ata de Posse da Diretoria registrada em RCPJ ou CNPJ com CPF correspondente no QSA da Receita Federal (Nível 1)
- **WHEN** o sistema confirma a titularidade jurídica
- **THEN** o status da igreja SHALL transitar para `Verified` com nível Nível 1
- **AND** o selo exibido no perfil público SHALL ser "Igreja Verificada Oficial"
- **AND** o sistema SHALL liberar a gestão de chave PIX, convite de novos administradores e atualização de dados cadastrais.

---

### P1: Métodos de Validação Básicos (Presença Física e Vínculo Digital) ⭐ MVP

#### Cenário 6: Reivindicação via Presença Física (Geofencing 100m + Foto em tempo real - Nível 3)
- **GIVEN** que o solicitante está fisicamente no templo da igreja com GPS aferido a 45 metros de distância das coordenadas oficiais
- **WHEN** o solicitante captura foto da fachada em tempo real pela câmera do aplicativo
- **THEN** o sistema SHALL aceitar as coordenadas e armazenar a imagem com carimbo temporal e logs
- **AND** aprovar a validação como Nível 3.

#### Cenário 7: Rejeição de presença física por distância fora do geofence
- **GIVEN** que as coordenadas cadastradas da igreja estão na localização X
- **AND** o solicitante está localizado a 450 metros de distância do templo
- **WHEN** o solicitante tenta capturar a foto de validação presencial
- **THEN** o sistema SHALL bloquear a captura
- **AND** informar que a validação presencial exige estar a menos de 100 metros do local oficial
- **AND** NÃO DEVE alterar o status da igreja.

---

### P2: Resolução Automática de Disputa e Contestação Paritária

**História de Usuário**: Como representante legal de uma igreja portando documentação registrada em Cartório (RCPJ) e CNPJ, quero que a apresentação desses documentos revogue sumariamente vínculos anteriores obtidos por métodos sociais ou institucionais sem bloqueio unilateral do titular anterior, assumindo a titularidade legítima do perfil.

**Por que P2**: Protege a soberania jurídica da congregação, impede extorsões ou bloqueios unilaterais por terceiros e garante a fé pública dos registros cartoriais.

#### Cenário 8: Resolução Automática de Disputa por Prevalência Documental Legal de Nível 1
- **GIVEN** que a igreja está no estado `Verified` sob posse de um usuário validado via Nível 2 (e-mail institucional) ou Nível 3 (social/GPS)
- **AND** o representante legal legítimo acessa a página pública da igreja e clica em "Contestar Propriedade"
- **WHEN** o representante legal anexa a Ata de Posse da Diretoria registrada em Cartório (RCPJ) e Cartão CNPJ/QSA válido (Nível 1)
- **AND** aceita os termos com declaração de legitimidade sob o art. 299 do Código Penal
- **THEN** o sistema SHALL validar a conformidade documental de Nível 1
- **AND** revogar sumariamente e de forma imediata o vínculo administrativo do detentor anterior (Nível 2 ou 3)
- **AND** transferir a titularidade da igreja para o representante legal de Nível 1 sob status `Verified`
- **AND** notificar o detentor anterior sobre a revogação sumária por prevalência documental legal
- **AND** NÃO DEVE conceder ao detentor anterior direito a bloqueio unilateral ou retenção do processo.

#### Cenário 9: Disputa paritária entre documentos de Nível 1 com congelamento imediato (In_Dispute)
- **GIVEN** que a igreja está no estado `Verified` sob titular validado em Nível 1
- **WHEN** um segundo solicitante também submete documentação formal de Nível 1 (Ata RCPJ/QSA) contestando a vigência da atual diretoria
- **THEN** o sistema SHALL alterar o status da igreja imediatamente para `In_Dispute`
- **AND** congelar qualquer edição nos horários, dados públicos, fotos e chave PIX
- **AND** notificar ambas as partes com prazo de 5 dias úteis para apresentação de certidões atualizadas de vigência de mandato no RCPJ
- **AND** exibir no perfil público o aviso "Perfil em processo de verificação de titularidade".

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
| CLAIM-02 | P1: Validação por Presença Física (Geofencing 100m + Foto ao vivo) - Nível 3 | Specify | Pendente |
| CLAIM-03 | P1: Validação por Redes Sociais Oficiais (Código na Bio) - Nível 3 | Specify | Pendente |
| CLAIM-04 | P1: Validação por Canais Institucionais Proprietários (Domínio Próprio / E-mail com OTP) - Nível 2 | Specify | Pendente |
| CLAIM-05 | P1: Validação Documental Pública e Cartorial (Ata de Posse RCPJ, Estatuto, CNPJ/QSA) - Nível 1 | Specify | Pendente |
| CLAIM-06 | P1: Termos de Uso, Declaração sob art. 299 CP e Isenção de Responsabilidade | Specify | Pendente |
| CLAIM-07 | P1: Trilha de Auditoria e Logs Obrigatórios conforme Marco Civil da Internet (Lei 12.965/2014) | Specify | Pendente |
| CLAIM-08 | P1: Níveis de Permissão por Hierarquia Probatória (Nível 1 pleno/PIX vs Níveis 2 e 3 operacionais) | Specify | Pendente |
| CLAIM-09 | P2: Tratamento de Concorrência, Precedência por Nível Probatório e Rate Limits | Specify | Pendente |
| CLAIM-10 | P2: Regra de Resolução Automática de Disputa (Prevalência de Nível 1 sobre Níveis 2 e 3 sem Bloqueio Unilateral) | Specify | Pendente |
| CLAIM-11 | P2: Contestação Paritária de Nível 1 e Congelamento em In_Dispute | Specify | Pendente |
| CLAIM-12 | P2: Resolução de Disputa, Análise de Tempestividade de Ata RCPJ e Transferência Segura | Specify | Pendente |

**Cobertura:** 12 requisitos estruturados, 0 mapeados para tarefas técnicas, 12 aguardando confirmação da especificação.

---

## Critérios de Sucesso

- [ ] 100% dos processos de reivindicação exigem assinatura eletrônica da declaração sob as penas do art. 299 do Código Penal e aceite de isenção da plataforma.
- [ ] 100% dos eventos de claim e disputa geram logs invioláveis com IP, porta lógica, timestamp UTC, user-agent e identificadores (atendendo ao Marco Civil da Internet).
- [ ] Usuários validados por métodos de Nível 3 ou Nível 2 não conseguem cadastrar chaves PIX, alterar dados cadastrais críticos ou transferir titularidade (recursos exclusivos de Nível 1).
- [ ] Contestações comprovadas com documentação válida de Nível 1 revogam sumariamente e de imediato vínculos obtidos via Nível 2 ou 3, notificando o titular anterior sem direito a bloqueio unilateral.
- [ ] Contestações concorrentes entre documentos de Nível 1 congelam imediatamente o perfil em `In_Dispute` para julgamento administrativo da ata de posse vigente no RCPJ em até 5 dias úteis.
