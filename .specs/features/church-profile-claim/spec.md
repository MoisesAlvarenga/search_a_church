# Especificação de Reivindicação de Perfil de Igreja (Claim)

## Problema

Milhares de igrejas existem no mapa e na base de dados de descoberta sem um gestor oficial vinculado (`Unclaimed`). Para transformar essas entidades em perfis ativos e confiáveis no produto, é necessário permitir que líderes e representantes comprovem sua legitimidade e assumam a gestão do perfil da igreja. Sem um mecanismo seguro de reivindicação, validação de vínculos e resolução de disputas, o sistema fica vulnerável a fraudes, sequestro de perfis comunitários e informações desatualizadas.

## Objetivos

- [ ] Permitir que representantes legítimos iniciem e concluam a reivindicação de igrejas exibidas na plataforma.
- [ ] Implementar ciclo de vida rigoroso com estados `Unclaimed`, `Pending_Verification`, `Verified` e `In_Dispute`.
- [ ] Oferecer múltiplos métodos de validação adaptáveis à realidade das comunidades (presença física, vínculo digital e comprovação documental/CNPJ).
- [ ] Definir níveis graduais de acesso e permissões conforme o estado da solicitação.
- [ ] Fornecer regras determinísticas para tratamento de solicitações concorrentes, rejeição com feedback claro e mediação de disputas de posse.

## Fora do Escopo

| Funcionalidade | Motivo |
| -------------- | ------ |
| Sistema de arrecadação financeira, doações ou dízimos online | Fora do escopo do produto principal; definido em módulo financeiro futuro. |
| Integração automatizada com sistemas de cartórios ou juntas comerciais | Validações documentais complexas utilizam análise documental assistida e consultas públicas de CNPJ. |
| Gestão interna de membros, escalas pastorais e voluntariado | Pertencem à administração interna da congregação, fora do MVP de descoberta e perfil. |
| Autenticação, recuperação de credenciais e MFA do usuário solicitante | Responsabilidade exclusiva de `authentication-authorization`. |
| Arbitragem judicial ou mediação presencial de conflitos | O sistema limita-se a regras administrativas de posse na plataforma com base em evidências formais. |

---

## Hipóteses e Questões em Aberto

| Hipótese / decisão | Padrão adotado | Justificativa | Confirmada? |
| ------------------ | -------------- | ------------- | ----------- |
| Prioridade probatória em conflitos | Documentos legais registrados (Ata/Estatuto/CNPJ) têm precedência hierárquica sobre validações digitais ou de presença física. | Segurança jurídica e proteção contra engenharia social. | Não |
| Tempo de expiração de reivindicação pendente | Reivindicações em `Pending_Verification` sem envio de provas expiram em 7 dias corridos. | Libera o perfil para novas tentativas caso o solicitante abandone o fluxo. | Não |
| Prazo de resposta em contestação (`In_Dispute`) | O titular atual do perfil tem 5 dias úteis para responder e enviar contraprovas quando uma disputa formal for aceita. | Garante direito de resposta sem paralisar indefinidamente o perfil. | Não |
| Raio de tolerância para geofencing | 100 metros a partir das coordenadas geográficas oficiais cadastradas da igreja. | Compensa margens de erro de GPS móvel em áreas urbanas sem comprometer a comprovação de presença física. | Não |
| Prevalência do primeiro solicitante | Em solicitações concorrentes sem envio de documentos legais prévios, o primeiro a submeter evidência válida coloca o perfil em lock de análise. | Evita múltiplos processos paralelos inconsistentes. | Não |

**Questões em aberto:** nenhuma. Todos os comportamentos críticos estão registrados como hipóteses e critérios de negócio acima.

---

## 1. Ciclo de Vida e Máquina de Estados

### Diagrama de Estados e Transições

```
  ┌─────────────────────────────────────────────────────────────┐
  │                                                             │
  ▼                                                             │
┌───────────┐      Início de Claim        ┌──────────────────────┐  Expiração / Reprovação Total
│ Unclaimed │ ──────────────────────────> │ Pending_Verification │ ───────────────────────────────┐
└───────────┘                             └──────────────────────┘                                │
      ▲                                         │                                                 │
      │                                         │ Aprovação das Provas                            │
      │                                         ▼                                                 │
      │                                   ┌───────────┐                                           │
      │   Fraude comprovada mútua /       │           │ <───────────────────┐                     │
      │   Revogação por perda de dados    │ Verified  │                     │ Disputa improcedente│
      │                                   └───────────┘                     │ / Titular ratificado│
      │                                         │                           │                     │
      │                                         │ Abertura de Contestação   │                     │
      │                                         ▼ com Documento Legal       │                     │
      │                                   ┌───────────┐                     │                     │
      └────────────────────────────────── │In_Dispute │ ────────────────────┘                     │
                                          └───────────┘                                           │
                                                │ Transferência de Titularidade                   │
                                                └─────────────────────────────────────────────────┘
```

### Tabela de Transições de Estado

| Estado Atual | Evento / Condição | Próximo Estado | Efeito no Sistema |
| ------------ | ----------------- | -------------- | ----------------- |
| `Unclaimed` | Solicitante autenticado inicia claim e submete o primeiro método de validação | `Pending_Verification` | Bloqueia novas solicitações concorrentes simples; habilita permissão restrita de edição preliminar. |
| `Pending_Verification` | Provas submetidas atingem o limiar de aprovação exigido | `Verified` | Concede administração integral ao solicitante; ativa selo de "Igreja Verificada"; notifica o representante. |
| `Pending_Verification` | Provas rejeitadas definitivamente ou prazo limite de 7 dias expirado sem envio | `Unclaimed` | Remove o vínculo preliminar do solicitante; descarta rascunhos não aprovados; libera o perfil para novo claim. |
| `Verified` | Terceiro abre contestação formal anexando comprovação documental registrada (CNPJ / Ata) | `In_Dispute` | Congela dados cadastrais sensíveis; notifica titular atual com prazo de 5 dias úteis para réplica. |
| `In_Dispute` | Análise documental julga procedente a contestação do novo requerente | `Verified` | Transfere a titularidade e o acesso administrativo ao novo representante comprovado legalmente. |
| `In_Dispute` | Contestação rejeitada por documentação inválida ou titular atual comprova vigência | `Verified` | Mantém a titularidade com o detentor atual e encerra o processo de disputa. |
| `In_Dispute` | Ambas as partes apresentam evidências fraudulentas ou a igreja foi extinta/desativada | `Unclaimed` | Revoga a administração de ambos e restaura a entidade ao estado neutro não reivindicado. |

---

## 2. Métodos de Validação

O sistema disponibiliza três vias independentes ou complementares de comprovação de legitimidade:

### Método A: Presença Física (Geofencing + Foto em Tempo Real)
- **Geofencing:** O dispositivo móvel do solicitante deve estar dentro de um raio de no máximo **100 metros** das coordenadas oficiais da igreja.
- **Foto em Tempo Real:** Captura obrigatória de foto através da câmera do aplicativo no momento da solicitação (bloqueado upload de fotos pré-existentes da galeria do aparelho).
- **Validação de Conteúdo:** Foto deve enquadrar a fachada do templo com letreiro identificável ou o interior da nave/púlpito.

### Método B: Vínculo Digital
- **B.1 - Código na Bio / Descrição de Redes Sociais Oficiais:**
  - O sistema gera um token alfanumérico único e temporário (ex.: `SAC-8492-VERIFY`).
  - O solicitante deve inserir esse código na bio/descrição do perfil oficial da igreja no Instagram, Facebook ou canal do YouTube cadastrado.
  - O sistema realiza checagem da bio.
- **B.2 - E-mail com Domínio Institucional Próprio:**
  - Envio de código OTP de 6 dígitos com validade de 15 minutos para endereço de e-mail cujo domínio coincida com o domínio oficial da igreja (ex.: `pastor@igrejabatistaesperanca.com.br`).

### Método C: Validação Documental e CNPJ
- **C.1 - Cruzamento Automático com Quadro de Sócios e Administradores (QSA):**
  - O solicitante informa o CNPJ da entidade religiosa e seu CPF.
  - O sistema cruza os dados com a base pública da Receita Federal. Havendo coincidência do CPF/Nome com o representante legal ou diretor registrado, a titularidade é validada com nível máximo de confiança.
- **C.2 - Análise de Documento Comprobatório Registrado:**
  - Caso o solicitante seja líder pastoral, secretário ou procurador não constante diretamente no QSA: upload de Ata de Eleição e Posse vigente registrada em Cartório de Registro Civil de Pessoas Jurídicas (RCPJ), Estatuto Social ou Procuração assinada pelo representante legal com poderes específicos.

---

## 3. Matriz de Níveis de Acesso por Estado

| Operação no Perfil | `Unclaimed` | `Pending_Verification` (Solicitante) | `Verified` (Titular Oficial) | `In_Dispute` (Partes Envolvidas) |
| ------------------ | ----------- | ------------------------------------ | ---------------------------- | -------------------------------- |
| Visualizar dados públicos e localização | Leitura pública | Leitura pública | Leitura pública | Leitura pública |
| Editar horários de cultos e reuniões | Bloqueado | Permitido (Rascunho preliminar) | Permitido (Publicação imediata) | Bloqueado temporariamente |
| Editar descrição, telefone público e estilos de culto | Bloqueado | Permitido (Rascunho preliminar) | Permitido (Publicação imediata) | Bloqueado temporariamente |
| Publicar fotos na galeria da comunidade | Bloqueado | Permitido (com selo preliminar) | Permitido | Bloqueado temporariamente |
| Alterar endereço físico ou coordenadas | Bloqueado | Bloqueado | Permitido (requer revalidação) | Bloqueado |
| Alterar CNPJ ou Razão Social | Bloqueado | Bloqueado | Permitido (requer revalidação) | Bloqueado |
| Responder avaliações de visitantes | Bloqueado | Bloqueado | Permitido com selo oficial | Bloqueado |
| Exibir selo de "Igreja Verificada" | Não | Não (Exibe "Reivindicação em análise") | Sim | Sim (com aviso de "Em contestação") |
| Transferir titularidade para outro usuário | Bloqueado | Bloqueado | Permitido | Bloqueado |
| Excluir perfil ou solicitar desativação | Bloqueado | Bloqueado | Permitido mediante confirmação | Bloqueado |

---

## 4. Casos de Borda e Tratamento de Falhas

### 4.1 Solicitações Simultâneas / Concorrentes
- Se o Usuário A iniciar claim para uma igreja `Unclaimed`, o sistema aplica lock e coloca a entidade em `Pending_Verification`.
- Se o Usuário B tentar iniciar claim para a mesma igreja:
  - O sistema informa que existe uma solicitação em análise em andamento.
  - O Usuário B pode optar por cadastrar-se para ser notificado caso a solicitação expire ou seja reprovada.
  - **Exceção de Prevalência Legal:** Se o Usuário B possuir documentação legal máxima (CNPJ + Ata de Posse registrada), ele pode solicitar a abertura de análise prioritária documental, sobrepondo-se à solicitação pendente baseada em métodos mais fracos (redes sociais ou foto).

### 4.2 Rejeição de Provas e Rate Limit
- Provas que não atendam aos requisitos (geofencing fora do raio, código incorreto na bio, documento ilegível ou vencido) são marcadas como `Rejected` com motivo claro e auditável.
- O solicitante tem até **3 tentativas de reenvio** dentro do período de 7 dias.
- Se atingir 3 rejeições consecutivas ou o prazo expirar, a solicitação é cancelada, o perfil retorna a `Unclaimed` e o solicitante fica bloqueado para novas tentativas naquela igreja por **72 horas**.

### 4.3 Contestação e Disputa de Posse (`In_Dispute`)
- Qualquer contestação de um perfil já `Verified` exige **obrigatoriamente** o envio de documento legal registrado em cartório (Ata de Posse, Estatuto ou CNPJ com QSA).
- Abertura de disputa congela alterações de dados sensíveis da igreja imediatamente.
- Notificação simultânea via e-mail e push para o atual titular e o contestante.
- Prazo de **5 dias úteis** para manifestação do titular atual com apresentação de suas contraprovas.
- Ao final do prazo ou após submissão de ambas as partes, a moderação analisa a validade registral dos documentos e emite a decisão vinculante.

---

## Histórias de Usuário e Critérios de Aceite (BDD)

### P1: Iniciar e Concluir Reivindicação por Vínculo Digital ⭐ MVP

**História de Usuário**: Como representante de uma igreja ainda não reivindicada, quero comprovar meu vínculo através das redes sociais oficiais ou e-mail de domínio próprio para assumir a gestão verificada do perfil.

**Por que P1**: Oferece o método com menor atrito operacional para adesão rápida de comunidades ativas digitalmente.

#### Cenário 1: Reivindicação bem-sucedida via código na bio de rede social
- **GIVEN** que a igreja "Comunidade da Fé" está com status `Unclaimed` no sistema
- **AND** possui a conta de Instagram `@comunidade_fe` listada publicamente em seus dados
- **AND** o usuário "Pastor João" está autenticado na plataforma
- **WHEN** o Pastor João solicita a reivindicação pelo método de Rede Social
- **THEN** o sistema SHALL gerar o código único `SAC-7721` com validade de 24 horas
- **AND** o status da igreja SHALL transitar para `Pending_Verification`
- **WHEN** o Pastor João insere o código `SAC-7721` na bio do `@comunidade_fe` e clica em "Verificar agora"
- **THEN** o sistema SHALL consultar a bio da rede social, identificar o código correspondente
- **AND** transitar o status da igreja para `Verified`
- **AND** atribuir ao Pastor João a permissão de administrador oficial da igreja.

#### Cenário 2: Falha por ausência do código na bio
- **GIVEN** que a solicitação de reivindicação está em `Pending_Verification` com o código `SAC-7721`
- **WHEN** o sistema executa a checagem e não encontra o código na bio do perfil informado
- **THEN** o sistema SHALL manter o status em `Pending_Verification`
- **AND** exibir mensagem de erro informando que o código não foi localizado
- **AND** registrar 1 tentativa consumida do limite de reenvios do solicitante.

---

### P1: Reivindicação por Presença Física no Local ⭐ MVP

**História de Usuário**: Como líder local de uma congregação sem presença digital forte, quero comprovar minha titularidade comparecendo ao templo físico e fotografando a igreja para ser verificado.

**Por que P1**: Garante inclusão de comunidades tradicionais ou de menor porte que não possuem site ou equipe de mídia.

#### Cenário 3: Validação de presença física com geofencing e foto ao vivo
- **GIVEN** que a congregação possui endereço e coordenadas geográficas conhecidas
- **AND** o solicitante está fisicamente no templo, com precisão de GPS dentro do raio de 100 metros
- **WHEN** o solicitante aciona a validação de presença física
- **AND** captura uma foto da fachada da igreja em tempo real através da câmera do aplicativo
- **THEN** o sistema SHALL validar que as coordenadas do dispositivo coincidem com o local da igreja
- **AND** salvar a imagem com carimbo temporal e metadados de auditoria
- **AND** submeter a prova para validação conclusiva
- **AND** transitar o status para `Verified` quando a análise de conformidade for aprovada.

#### Cenário 4: Rejeição de presença física por distância fora do geofence
- **GIVEN** que as coordenadas cadastradas da igreja estão na localização X
- **AND** o solicitante está localizado a 450 metros de distância do templo
- **WHEN** o solicitante tenta capturar a foto de validação presencial
- **THEN** o sistema SHALL bloquear a captura
- **AND** informar que a validação presencial exige estar a menos de 100 metros do local oficial
- **AND** NÃO DEVE alterar o status da igreja.

---

### P1: Validação Documental e CNPJ ⭐ MVP

**História de Usuário**: Como representante legal de uma organização religiosa, quero validar o perfil da igreja pelo CNPJ oficial para obter status verificado imediato com segurança jurídica.

**Por que P1**: É a fonte canônica máxima de verdade para resolução de titularidade e prevenção a fraudes.

#### Cenário 5: Verificação direta via cruzamento de CPF com QSA do CNPJ
- **GIVEN** que a igreja possui CNPJ informado
- **AND** o solicitante possui CPF validado em sua conta
- **WHEN** o solicitante seleciona validação por CNPJ
- **THEN** o sistema SHALL consultar o Quadro de Sócios e Administradores (QSA) da Receita Federal
- **AND** identificar que o CPF do solicitante corresponde ao Presidente/Representante legal registrado
- **AND** transitar imediatamente o status da igreja para `Verified`
- **AND** conceder titularidade plena ao solicitante.

#### Cenário 6: Upload de documento comprobatório para líder não listado no QSA
- **GIVEN** que o solicitante é o pastor titular local, mas o CNPJ institucional está em nome da convenção geral
- **WHEN** o solicitante anexa o PDF da Ata de Posse da congregação com firma reconhecida e registro em RCPJ
- **THEN** o sistema SHALL salvar o documento de forma criptografada
- **AND** manter o status em `Pending_Verification`
- **AND** encaminhar para fila prioritária de conferência documental
- **AND** notificar o solicitante sobre o prazo de análise de até 48 horas úteis.

---

### P2: Controle de Acesso Preliminar durante Análise

**História de Usuário**: Como solicitante com verificação pendente, quero poder atualizar horários de cultos e contatos para adiantar as informações da igreja enquanto aguardo a aprovação final.

**Por que P2**: Evita desperdício de tempo e engaja o solicitante durante o período de triagem de evidências.

#### Cenário 7: Edição permitida em estado pendente (dados não sensíveis)
- **GIVEN** que o solicitante possui uma reivindicação ativa em estado `Pending_Verification`
- **WHEN** o solicitante edita os horários de cultos de domingo e adiciona a descrição da congregação
- **THEN** o sistema SHALL aceitar e armazenar as edições em rascunho preliminar
- **AND** permitir a pré-visualização das informações.

#### Cenário 8: Bloqueio de edição sensível em estado pendente
- **GIVEN** que a solicitação está em estado `Pending_Verification`
- **WHEN** o solicitante tenta alterar o endereço físico, o CNPJ ou responder a uma avaliação pública de visitante
- **THEN** o sistema SHALL rejeitar a operação
- **AND** exibir mensagem informando que alterações cadastrais estruturais exigem verificação concluída (`Verified`).

---

### P2: Mediação de Disputas de Posse (In_Dispute)

**História de Usuário**: Como legítimo presidente de uma congregação cujo perfil foi indevidamente verificado por outra pessoa, quero abrir uma disputa formal com apresentação de ata registrada para recuperar a posse do perfil.

**Por que P2**: Essencial para a integridade do ecossistema e proteção contra apropriações indevidas.

#### Cenário 9: Abertura de disputa com documento legal
- **GIVEN** que a igreja está no estado `Verified` sob posse do Usuário Antigo
- **AND** o Novo Requerente submete contestação anexando Ata de Posse registrada em cartório e CNPJ
- **WHEN** o sistema valida que a documentação formal de contestação foi anexada com êxito
- **THEN** o sistema SHALL alterar o status da igreja para `In_Dispute`
- **AND** congelar a edição de dados sensíveis da igreja
- **AND** notificar o Usuário Antigo com prazo improrrogável de 5 dias úteis para defesa
- **AND** exibir publicamente um aviso informativo de que a titularidade está sob revisão.

#### Cenário 10: Resolução da disputa com transferência de titularidade
- **GIVEN** que a igreja está em `In_Dispute`
- **AND** a análise comprova que a documentação do Novo Requerente é legítima e revoga a legitimidade do Usuário Antigo
- **WHEN** a decisão da disputa é registrada
- **THEN** o sistema SHALL revogar os privilégios administrativos do Usuário Antigo
- **AND** transferir a titularidade e o acesso administrativo ao Novo Requerente
- **AND** restaurar o status da igreja para `Verified`
- **AND** remover o aviso público de disputa.

---

## Casos de Borda Adicionais

- **QUANDO** um solicitante tentar reivindicar uma igreja que já possui outro claim em `Pending_Verification` **THEN** o sistema SHALL impedir novo claim paralelo e permitir ao segundo usuário inscrever-se para aviso de disponibilidade ou apresentar contestação legal prioritária.
- **QUANDO** uma solicitação em `Pending_Verification` permanecer inativa sem envio de novas evidências por 7 dias corridos **THEN** o sistema SHALL expirar o processo, descartar rascunhos não publicados e retornar a igreja ao estado `Unclaimed`.
- **QUANDO** um usuário tiver sua conta banida ou suspensa em `authentication-authorization` **THEN** quaisquer claims ativos ou perfis verificados sob sua gestão SHALL ser congelados ou transferidos para `In_Dispute` / `Unclaimed` conforme determinação de segurança.
- **QUANDO** a consulta externa de QSA ou verificação de redes sociais sofrer instabilidade ou timeout **THEN** o sistema SHALL enfileirar a checagem para retentativa assíncrona com fallback manual e NÃO DEVE rejeitar a solicitação do usuário sumariamente.

---

## Dimensões de Requisitos Implícitos (Sweep)

| Dimensão | Cobertura na Especificação |
| -------- | -------------------------- |
| **Validação de Entrada e Limites** | Raio de geofencing de 100m; formato de CNPJ; fotos limitadas a captura em tempo real; expiração de OTP de 15 min; token de bio de 24h. |
| **Estados de Falha e Parciais** | Retorno a `Unclaimed` em cancelamento; preservação de rascunhos em staging até efetivação do status `Verified`. |
| **Idempotência e Concorrência** | Lock atômico ao transitar para `Pending_Verification`; proibição de claims concorrentes simultâneos sem contestação formal documental. |
| **Fronteiras de Autenticação e Rate Limits** | Máximo de 3 tentativas de envio de evidências por solicitação; bloqueio temporário de 72h após 3 reprovações consecutivas; usuário deve estar previamente autenticado. |
| **Ciclo de Vida de Dados e Expiração** | Documentos enviados são arquivados com retenção auditável segura; expiração automática de pendências em 7 dias sem atividade. |
| **Observabilidade e Auditoria** | Registro em trilha de auditoria (timestamp, IP, método, evidência utilizada, avaliador) para toda mudança de estado entre `Unclaimed`, `Pending_Verification`, `Verified` e `In_Dispute`. |
| **Falha de Dependências Externas** | Fallback assíncrono para indisponibilidade de APIs públicas de CNPJ ou scraping de redes sociais com fila de conferência. |
| **Integridade de Transição de Estado** | Apenas as 7 transições autorizadas na máquina de estados são aceitas; bloqueio de saltos diretos ilegais (ex.: `Unclaimed` para `In_Dispute`). |

---

## Rastreabilidade de Requisitos

| ID do Requisito | História / Área | Fase | Status |
| --------------- | --------------- | ---- | ------ |
| CLAIM-01 | P1: Ciclo de Vida e Estados (Máquina de Estados e Transições) | Specify | Pendente |
| CLAIM-02 | P1: Validação por Presença Física (Geofencing 100m + Foto ao vivo) | Specify | Pendente |
| CLAIM-03 | P1: Validação por Vínculo Digital (Código na Bio de Redes Sociais) | Specify | Pendente |
| CLAIM-04 | P1: Validação por Vínculo Digital (E-mail com Domínio Institucional / OTP) | Specify | Pendente |
| CLAIM-05 | P1: Validação Documental e CNPJ (Cruzamento com QSA e Análise de Ata RCPJ) | Specify | Pendente |
| CLAIM-06 | P2: Controle de Acesso por Estado (Permissões restritas em Pending vs Verified) | Specify | Pendente |
| CLAIM-07 | P2: Tratamento de Concorrência, Rate Limits e Rejeição de Provas | Specify | Pendente |
| CLAIM-08 | P2: Mediação de Disputas de Posse (Abertura, Congelamento e Resolução de `In_Dispute`) | Specify | Pendente |

**Cobertura:** 8 requisitos estruturados, 0 mapeados para tarefas técnicas, 8 aguardando confirmação da especificação.

---

## Critérios de Sucesso

- [ ] 100% das transições de posse de perfil de igreja obedecem estritamente aos estados `Unclaimed`, `Pending_Verification`, `Verified` e `In_Dispute`.
- [ ] Representantes legítimos conseguem validar a congregação através de ao menos um dos três métodos suportados (físico, digital ou documental).
- [ ] Nenhum usuário não verificado consegue alterar dados sensíveis (endereço, CNPJ, razão social) ou publicar em nome oficial da igreja.
- [ ] Tentativas concorrentes de claim e contestações de perfis existentes possuem comportamento determinístico com garantia de primazia documental e ampla defesa em 5 dias úteis.
- [ ] Todas as ações de reivindicação e disputa geram trilha completa de auditoria imutável.
