# Especificação de Gestão de Perfis

## Problema

A relevância da busca depende das preferências do usuário e das características da igreja. O produto precisa de perfis simples, consistentes e reutilizáveis para evitar a repetição de preferências, permitir correspondência multidimensional de afinidade e viabilizar filtros avançados baseados em recursos específicos (como acessibilidade com rampas ou LIBRAS, berçário e estacionamento).

## Objetivos

- [ ] Usuários podem criar, consultar e atualizar um perfil com preferências litúrgicas, teológicas e tags especializadas.
- [ ] Representantes verificados de igrejas podem cadastrar e manter os dados estruturados e atributos de suas congregações.
- [ ] Usuários e igrejas compartilham um catálogo padronizado de tags (acessibilidade, facilidades e ministérios) para refinamento da busca.
- [ ] Perfis de igrejas podem ser criados a partir da vinculação com uma localização externa do Google Maps (`place_id`).
- [ ] Integridade e propriedade são rigorosamente protegidas: usuários só editam seus próprios dados e congregações só são editadas por representantes com credencial `Verified`.
- [ ] Ciclo de vida com suporte a soft delete para conformidade com a LGPD e preservação histórica de entidades eclesiásticas.

## Fora do Escopo

| Funcionalidade | Motivo |
| -------------- | ------ |
| Gestão administrativa interna de membros e tesouraria | Fora do escopo do produto principal. |
| Ciclo de vida da identidade e tokens de autenticação | Definido em `authentication-authorization`. |
| Ranqueamento e fórmula matemática de relevância | Definida em `search-discovery`. |
| Renderização do mapa e consultas à API externa | Definida em `maps-integration`. |
| Rito probatório e disputas de liderança | Definido em `church-profile-claim`. |

---

## Hipóteses e Questões em Aberto

| Hipótese / decisão | Padrão adotado | Justificativa | Confirmada? |
| ------------------ | -------------- | ------------- | ----------- |
| Edição do perfil da igreja | Gestão e alteração de dados da congregação são restritas exclusivamente a representantes autenticados com status `Verified` concedido via `church-profile-claim`. Edição de perfil de usuário é restrita ao próprio titular. | Confirmado em AD-008, AD-009 e AD-026: segurança contra manipulação de dados eclesiásticos e privacidade pessoal. | Sim |
| Campos do perfil de usuário | Preferências opcionais: denominação/linha teológica, idiomas do culto, estilo litúrgico, tipos de ministério, raio padrão (`radiusKm`) e tags de preferências (acessibilidade, facilidades e ministérios). Ausência de dados assume baseline neutra. | Confirmado em AD-020, AD-021 e AD-026: alimenta afinidade teológica sem bloquear novos usuários. | Sim |
| Campos do perfil de igreja | Obrigatórios: nome, endereço formatado, coordenadas (lat/long) e denominação. Opcionais: horários de reuniões, idiomas, estilo de liturgia, contatos institucionais, tags oferecidas e `place_id`. | Confirmado em AD-026: dados mínimos garantem localização espacial e comparação justa na busca. | Sim |
| Vinculação com Google Maps | Associação determinística 1:1 entre o perfil oficial e o `place_id`. Tentativa de reutilizar identificador já associado a outro perfil ativo retorna erro `PLACE_ID_JA_VINCULADO`. | Confirmado em AD-004, AD-023, AD-025 e AD-026: unicidade de entidades físicas no mapa. | Sim |
| Exclusão de perfil e ciclo de vida | Exclusão lógica (*soft delete*) e anonimização de dados pessoais do usuário sob a LGPD; perfil de igreja adota inativação (`Inactive`) ou reversão para `Unclaimed` sem exclusão física do histórico de comunidade. | Confirmado em AD-026: conformidade legal com privacidade pessoal e integridade da base de dados comunitária. | Sim |
| Perfil salvo versus filtros diretos | Preferências salvas no perfil atuam como baseline padrão da busca; filtros aplicados na tela de busca são estritamente efêmeros e nunca alteram o perfil do usuário (edição exclusiva via perfil). | Confirmado em AD-021 e AD-026: previsibilidade da busca e controle deliberado de preferências. | Sim |
| Sistema Bidirecional de Tags | Catálogo unificado e padronizado de tags especializadas (Acessibilidade: rampa, LIBRAS, banheiro adaptado; Facilidades: estacionamento, berçário/kids; Ministérios: jovens, casais, EBD) selecionáveis por usuários e congregações. | Confirmado em AD-026: viabiliza correspondência refinada de necessidades e filtros avançados. | Sim |

**Questões em aberto:** nenhuma. Todas as 7 hipóteses de gestão de perfis estão integralmente confirmadas pelo responsável do produto.

---

## Histórias de Usuário

### P1: Salvar e Recuperar Preferências de Busca do Usuário MVP

**História de Usuário**: Como usuário autenticado, quero salvar minhas preferências teológicas, litúrgicas e tags de necessidades (como acessibilidade e facilidades) para que minhas buscas reflitam automaticamente minhas expectativas sem necessidade de redigitação.

**Por que P1**: Preferências salvas são a fonte primária de afinidade teológica (45% da relevância) no MVP.

**Critérios de Aceitação**:

1. QUANDO um usuário autenticado enviar preferências válidas (denominação, idiomas, estilos de culto, ministérios, raio de busca e tags desejadas como rampa, LIBRAS, estacionamento ou espaço kids) ENTÃO o sistema DEVE salvar o perfil associado ao seu `user_id`.
2. QUANDO um usuário consultar seu perfil salvo ENTÃO o sistema DEVE retornar suas preferências e tags ou resultado explícito de não configurado.
3. QUANDO um campo ou tag de preferência for inválido ou exceder limites aceitos ENTÃO o sistema DEVE rejeitar a gravação e identificar o campo incorreto.
4. QUANDO um usuário autenticado tentar alterar o perfil de outro usuário ENTÃO o sistema DEVE negar a solicitação com erro HTTP 403 `ACESSO_NEGADO_PROPRIEDADE`.
5. QUANDO um usuário aplicar filtros manuais na busca ENTÃO o sistema NÃO DEVE alterar nem persistir tais modificações no perfil de usuário salvo.

**Teste Independente**: Salvar perfil com tags de acessibilidade, recuperar os dados, tentar alterar com token de outro usuário e comprovar a rejeição por ownership.

---

### P1: Cadastrar e Atualizar Informações da Igreja por Representante Verificado MVP

**História de Usuário**: Como representante verificado de uma congregação, quero cadastrar e atualizar dados eclesiásticos, horários e tags de infraestrutura/acessibilidade da igreja para que o público encontre informações precisas.

**Por que P1**: Dados autênticos de congregações alimentam a descoberta e o mapa oficial do produto.

**Critérios de Aceitação**:

1. QUANDO um representante autenticado com credencial `Verified` enviar dados da igreja ENTÃO o sistema DEVE salvar nome, endereço formatado, coordenadas geográficas, denominação, horários, contatos e tags oferecidas.
2. QUANDO um usuário sem credencial `Verified` tentar criar ou alterar perfil de igreja ENTÃO o sistema DEVE negar a solicitação com erro HTTP 403 `REPRESENTANTE_NAO_VERIFICADO`.
3. QUANDO as coordenadas geográficas ou o endereço estiverem ausentes ou inválidos ENTÃO o sistema DEVE rejeitar o cadastro com validação explícita.
4. QUANDO o cadastro de igreja for iniciado a partir de local do Google Maps ENTÃO o sistema DEVE associar determinística e exclusivamente o `place_id` ao perfil da congregação.
5. QUANDO uma tentativa de cadastro ou atualização utilizar um `place_id` já vinculado a outro perfil ativo ENTÃO o sistema DEVE rejeitar a operação com erro HTTP 409 `PLACE_ID_JA_VINCULADO`.

**Teste Independente**: Submeter perfil de igreja com campos obrigatórios e tags por usuário verificado, tentar duplicar o mesmo `place_id` e comprovar o bloqueio por colisão.

---

### P2: Catálogo e Sistema Bidirecional de Tags

**História de Usuário**: Como usuário ou líder de igreja, quero selecionar tags padronizadas de acessibilidade, infraestrutura e ministérios para que a plataforma conecte necessidades específicas a recursos reais existentes.

**Por que P2**: Diferenciação inclusiva que facilita a busca de pessoas com necessidades específicas (PCDs, famílias com bebês, etc.).

**Critérios de Aceitação**:

1. QUANDO o sistema listar o catálogo de tags disponíveis ENTÃO DEVE expor vocabulário padronizado dividido em Acessibilidade (ex.: `rampa_acesso`, `interprete_libras`, `banheiro_acessivel`), Infraestrutura (ex.: `estacionamento_proprio`, `ar_condicionado`, `espaco_kids_bercario`) e Ministérios (ex.: `ministerio_jovens`, `ministerio_infantil`, `escola_biblica`).
2. QUANDO uma igreja cadastrar tags oferecidas ENTÃO o sistema DEVE validá-las contra o catálogo oficial e associá-las aos atributos pesquisáveis do templo.
3. QUANDO um usuário selecionar tags preferidas no perfil ENTÃO o sistema DEVE disponibilizá-las para cruzamento positivo na fórmula de pontuação da busca.

**Teste Independente**: Associar tags de LIBRAS e rampa a uma congregação e verificar que são recuperadas no perfil público para matching de busca.

---

### P2: Ciclo de Vida e Desativação de Perfil (Soft Delete e Conformidade LGPD)

**História de Usuário**: Como titular de perfil, quero poder encerrar ou desativar minha conta com garantia de privacidade e conformidade com a LGPD.

**Por que P2**: Conformidade legal obrigatória e proteção da integridade histórica da plataforma.

**Critérios de Aceitação**:

1. QUANDO um usuário solicitar a exclusão de sua conta ENTÃO o sistema DEVE executar soft delete, revogar todas as sessões ativas e anonimizar dados pessoais identificáveis, mantendo avaliações com autoria anônima despersonalizada.
2. QUANDO um perfil de igreja necessitar ser desativado por encerramento de atividades ENTÃO o sistema DEVE atualizar seu status para `Inactive`, ocultando-o da descoberta sem destruição física do registro.
3. QUANDO um representante for descredenciado ou revogado por processo de takedown ENTÃO o sistema DEVE desacoplar o gestor e reverter o status da congregação para `Unclaimed`.

**Teste Independente**: Solicitar encerramento de conta de usuário e comprovar a invalidação de acesso e despersonalização cadastral.

---

## Casos de Borda

- QUANDO o cadastro tentar vincular um `place_id` que já pertence a outro perfil ativo ENTÃO o sistema DEVE rejeitar a vinculação duplicada com erro `PLACE_ID_JA_VINCULADO`.
- QUANDO o usuário submeter tags customizadas não reconhecidas no catálogo ENTÃO o sistema DEVE rejeitar o payload com lista explicativa de valores suportados.
- QUANDO dois representantes tentarem atualizar o mesmo perfil de igreja concorrentemente ENTÃO o sistema DEVE aplicar controle de concorrência otimista (etag ou timestamp de versão) rejeitando a gravação defasada.
- QUANDO um usuário não possuir preferências salvas e realizar uma busca ENTÃO o sistema DEVE aplicar pontuação de baseline neutra sem falhas.

---

## Rastreabilidade de Requisitos

| ID do Requisito | História / Área | Fase | Status |
| --------------- | --------------- | ---- | ------ |
| PROFILE-01 | P1: Salvar e Recuperar Preferências de Busca do Usuário (Teologia, Liturgia, Raio e Tags - AD-026) | Specify | Confirmado |
| PROFILE-02 | P1: Autorização Estrita e Ownership do Perfil de Usuário (AD-008 e AD-021) | Specify | Confirmado |
| PROFILE-03 | P1: Cadastrar e Atualizar Informações da Igreja por Representante Verificado (AD-009 e AD-026) | Specify | Confirmado |
| PROFILE-04 | P1: Vinculação Determinística e Proteção de Unicidade do `place_id` (AD-004 e AD-026) | Specify | Confirmado |
| PROFILE-05 | P2: Sistema Bidirecional de Tags (Acessibilidade, Infraestrutura e Ministérios - AD-026) | Specify | Confirmado |
| PROFILE-06 | P2: Ciclo de Vida, Soft Delete e Conformidade LGPD (Anonimização e Status Inactive - AD-026) | Specify | Confirmado |

**Cobertura:** 6 requisitos estruturados, 6 confirmados com critérios BDD e decisões arquiteturais vinculadas, 0 pendentes de especificação. Prontos para Design.

---

## Critérios de Sucesso

- [ ] Preferências de busca do usuário (incluindo tags de acessibilidade e liturgia) são persistidas e reutilizáveis na busca.
- [ ] O catálogo padronizado de tags permite correspondência refinada entre o perfil do usuário e os recursos da igreja.
- [ ] Alterações cadastrais de congregações são restritas a representantes verificados (`Verified`).
- [ ] Cada congregação física possui vínculo único e determinístico com seu respectivo `place_id`.
- [ ] Encerramento de contas de usuário cumpre soft delete e anonimização conforme a LGPD.
- [ ] A base mantém integridade histórica sem exclusões destrutivas de templos.