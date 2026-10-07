# Especificação de Autenticação, Autorização e Sessão

## Problema

Perfis, feedback e a experiência de busca no mapa contêm dados e consomem recursos sensíveis da plataforma. O produto deve proteger escritas e o acesso aos dados geográficos do mapa, exigindo que o usuário esteja devidamente cadastrado e autenticado via JWT para acessar e buscar no mapa. Ao mesmo tempo, para oferecer uma experiência fluida sem fricção de login constante (como no Instagram, Spotify e Uber), o sistema deve gerenciar sessões contínuas e persistentes de forma segura, com rotação automática de tokens, detecção anti-roubo e blindagem contra ataques de força bruta e abuso por meio de limites de taxa inteligentes.

## Objetivos

- [x] A busca no mapa e o consumo de dados georreferenciados exigem usuário cadastrado e autenticado.
- [x] Ações protegidas e escritas exigem identidade autenticada via JWT.
- [x] Implementar autenticação contínua através de pares de tokens com Rotação Automática (Refresh Token Rotation - RTR) e Expiração Deslizante (Sliding Expiration de 60 dias).
- [x] Proteger a aplicação contra roubo e interceptação de tokens através da Detecção Automática de Reúso (Automatic Breach Detection), revogando imediatamente toda a família de tokens (`family_id`).
- [x] Exigir armazenamento de credenciais no hardware seguro do dispositivo mobile (iOS Keychain e Android Keystore) e interceptor de rede com fila de espera (*request queuing*).
- [x] Proteger as rotas de autenticação contra força bruta, *credential stuffing* e criação abusiva de contas através de Rate Limiting com algoritmo Sliding Window Counter no Redis e cabeçalhos HTTP 429 padronizados.
- [x] Definir critérios estritos e determinísticos de encerramento de sessão (logout).

## Fora do Escopo

| Funcionalidade | Motivo |
| -------------- | ------ |
| Escolha do provedor de identidade externo (OAuth social, Google/Apple Sign-in) | Decisão de Design / fase posterior. |
| Assinatura, pagamento ou faturamento de organizações | Fora do escopo do MVP. |
| Biometria avançada (FaceID/Fingerprint) como substituto de senha | O hardware armazena os tokens de forma segura; biometria local no app é refinamento de UI/UX. |
| Regras de conteúdo de perfis e avaliações | Definidas nas especificações correspondentes. |

---

## Hipóteses e Questões em Aberto

| Hipótese / decisão | Padrão adotado | Justificativa | Confirmada? |
| ------------------ | -------------- | ------------- | ----------- |
| Abordagem de autenticação | Autenticação via JWT (JSON Web Token) sem estado para as APIs protegidas. | Decisão de arquitetura confirmada pelo responsável do produto. | Sim |
| Acesso à busca por mapa | O acesso à visualização do mapa e consultas de busca georreferenciadas (`GET`) exige usuário cadastrado e autenticado via JWT (não são públicas). | Decisão confirmada pelo responsável do produto: controlar recursos de geolocalização e engajar usuários autenticados. | Sim |
| Criação de perfil de usuário (Sign-up) | O cadastro inicial / criação de perfil de usuário não exige autenticação prévia, sendo o ponto de entrada e primeiro contato do usuário com o produto. | Permite o onboarding fluido de novos usuários sem barreiras prévias de acesso. | Sim |
| Escritas protegidas e titularidade (Ownership) | Alterações e exclusões no perfil de usuário exigem autenticação via JWT e são restritas exclusivamente ao próprio usuário proprietário. Demais escritas (avaliações, cadastros) exigem autenticação. | Garante a integridade e privacidade dos dados, impedindo modificações por terceiros. | Sim |
| Autorização de perfil de igreja | O usuário deve ser obrigatoriamente um representante verificado (status `Verified` concedido via `church-profile-claim`) para efetuar qualquer tipo de alteração no perfil da igreja. | Decisão confirmada pelo responsável do produto: protege os dados eclesiásticos contra alterações não autorizadas. | Sim |
| Ciclo de vida e renovação contínua de tokens | Pares de tokens com Rotação Automática (RTR) e Expiração Deslizante (Sliding Expiration): Access Token JWT (15 min) + Refresh Token stateful (60 dias) com detecção de reúso e revogação por família (`family_id`). | Padrão robusto de mercado (Instagram, Spotify, Uber) que equilibra segurança e conveniência sem desconexão de usuários ativos. | Sim |
| Abuso e limites de taxa (Rate Limiting) | Rate limiting granular via Sliding Window Counter com Redis: `POST /auth/login` (5 falhas/15 min por IP+email e 50 req/hora global por IP), `POST /auth/register` (3 contas/hora por IP) e `POST /auth/refresh` (20 req/min por user_id+device_id), retornando HTTP 429 com headers RateLimit e Retry-After. | Proteção essencial contra força bruta, credential stuffing, DoS e criação em massa de contas. | Sim |

**Questões em aberto:** nenhuma. Todos os comportamentos críticos de autenticação, autorização, sessão e segurança contra abusos estão confirmados.

---

## 1. Arquitetura de Tokens e Sessão Contínua

### 1.1 Estrutura dos Tokens

1. **Access Token (JWT):**
   - **Formato:** Token JWT assinado criptograficamente, stateless.
   - **Tempo de Vida:** Curta duração (**15 minutos**).
   - **Claims Mínimas:** `sub` (ID do usuário), `exp` (expiração), `iat` (emissão), `jti` (identificador único do token), `family_id` (identificador da cadeia de rotação).
   - **Transmissão:** Enviado pelo cliente no cabeçalho HTTP `Authorization: Bearer <access_token>`.

2. **Refresh Token (Opaque Token Stateful):**
   - **Formato:** String aleatória de alta entropia (criptograficamente segura). O backend armazena exclusivamente o **hash SHA-256** do token.
   - **Tempo de Vida:** Longa duração (**60 dias** a partir da emissão/renovação).
   - **Associações Obrigatórias no Banco:** `user_id`, `device_id`, `family_id`, status (`Active`, `Consumed`, `Revoked`), data de expiração, timestamp de criação e metadados de auditoria (IP e User-Agent).

### 1.2 Mecanismo de Renovação Silenciosa (Silent Refresh)

- **Endpoint de Renovação:** `POST /auth/refresh`
  - **Payload:** `{ "refreshToken": "<string>", "deviceId": "<string>" }`
- **Uso Único e Rotação Estrita (One-Time Use):**
  - Cada Refresh Token só pode ser utilizado uma única vez.
  - Ao ser apresentado com sucesso na rota de refresh, o Refresh Token atual é imediatamente marcado como `Consumed`.
  - O backend gera instantaneamente um **novo par de tokens**:
    - Um novo Access Token (validade de 15 minutos);
    - Um novo Refresh Token (marcado como `Active`, herdando o mesmo `family_id`).
- **Expiração Deslizante (Sliding Expiration):**
  - Cada renovação bem-sucedida renova a janela de inatividade para **+60 dias** para o novo Refresh Token.
  - Se o usuário abrir o aplicativo periodicamente dentro da janela de 60 dias, a sessão é renovada de forma contínua e silenciosa, sem nunca deslogar o usuário.

### 1.3 Detecção Automática de Reúso e Segurança Anti-Roubo (Automatic Breach Detection)

A rotação de Refresh Tokens oferece um sensor intrínseco de roubo de credenciais:
1. Se um Refresh Token que já foi previamente consumido (status `Consumed` ou `Revoked`) for apresentado à rota `POST /auth/refresh`, o sistema interpreta o evento como tentativa de violação (replay attack resultante de interceptação ou vazamento de credenciais).
2. **Revogação Imediata por Família:**
   - O sistema localiza imediatamente a família de tokens (`family_id`) correspondente.
   - Todos os Refresh Tokens ativos dessa família são **revogados sumariamente** (`Revoked`).
   - Todos os Access Tokens dessa cadeia perdem a capacidade de renovação.
3. **Desconexão Forçada:** A resposta da API retorna `HTTP 401 Unauthorized` com código de erro explícito de violação (`TOKEN_BREACH_DETECTED`), forçando o encerramento da sessão tanto no dispositivo legítimo quanto no potencial invasor, exigindo novo login completo com credenciais.
4. **Alerta de Auditoria:** O evento de violação é registrado na trilha de auditoria contendo IP, dispositivo e timestamp da tentativa anômala.

### 1.4 Requisitos para o Cliente Mobile (App)

1. **Armazenamento Seguro em Hardware:**
   - O Refresh Token e credenciais de sessão do usuário DEVEM ser armazenados exclusivamente no **iOS Keychain** (iOS) e **Android Keystore / EncryptedSharedPreferences** (Android).
   - É terminantemente proibido o armazenamento de Refresh Tokens em texto plano, SharedPreferences não encriptado, arquivos desprotegidos ou LocalStorage web sem proteção.
2. **Interceptor HTTP com Fila de Espera (Request Queuing):**
   - O cliente HTTP no aplicativo deve possuir um interceptor de rede que monitora respostas da API.
   - Ao receber `HTTP 401 Unauthorized` em qualquer requisição protegida:
     a) O interceptor retém e enfileira todas as requisições em paralelo em memória;
     b) Bloqueia novas chamadas protegidas temporariamente;
     c) Executa uma única chamada silenciosa para `POST /auth/refresh` utilizando o Refresh Token armazenado no Keychain/Keystore;
     d) Ao receber o novo par de tokens com sucesso, atualiza o armazenamento seguro;
     e) Desbloqueia a fila e reenvia todas as requisições pausadas com o novo Access Token, de forma imperceptível para o usuário;
     f) Se o refresh falhar (token expirado ou violação detectada), a fila é rejeitada, o armazenamento seguro é limpo e a navegação redireciona o usuário para a tela de login.

### 1.5 Critérios Estritos de Logout

O usuário permanece autenticado continuamente, sendo desconectado e enviado à tela de login **exclusivamente** nos seguintes cenários:
1. **Inatividade Prolongada:** O usuário passa mais de **60 dias corridos** sem abrir o aplicativo (o Refresh Token expira naturalmente).
2. **Logout Manual (Stateless Puro - AD-024):** O usuário aciona voluntariamente a opção "Sair da Conta" no app. O cliente mobile chama `POST /auth/logout`, o backend revoga imediatamente o Refresh Token no banco de dados e o app apaga as credenciais locais do Keychain/Keystore. O Access Token residual expira naturalmente em até 15 minutos sem necessidade de denylist em memória, garantindo alta performance e simplicidade operacional.
3. **Alteração de Senha ou "Desconectar de Outros Dispositivos":** O usuário redefine sua senha ou solicita encerramento de sessões ativas. O backend revoga **todas as famílias de tokens** associadas ao `user_id`.
4. **Violação de Segurança:** Detecção automática de reúso de token consumido (Automatic Breach Detection), revogando a família de tokens.

---

## 2. Abuso e Limites de Taxa (Rate Limiting)

Para proteger a plataforma contra ataques de força bruta, sequestro de contas por *credential stuffing*, DoS e criação massiva de contas falsas, a camada de autenticação implementa controle estrito de limites de taxa.

### 2.1 Algoritmo e Armazenamento em Memória

- **Algoritmo:** **Sliding Window Counter** (Contador de Janela Deslizante). Combina a eficiência em memória do contador com a precisão temporal da janela deslizante, eliminando o problema de rajadas na borda (*boundary burst*) comum em janelas fixas.
- **Armazenamento:** **Redis** em memória. Executa scripts Lua atômicos para cálculo da taxa ponderada e incremento sem gerar bloqueios de concorrência ou condições de corrida (*race conditions*).

### 2.2 Regras Granulares por Rota de Autenticação

| Endpoint | Chave de Limitação | Limite Máximo | Janela de Tempo | Objetivo de Segurança |
| :--- | :--- | :---: | :---: | :--- |
| **`POST /auth/login`** (Tentativas Falhas) | Chave Composta `(IP + email)` | **5 falhas** | **15 minutos** | Impede ataques de força bruta direcionados a uma conta específica. |
| **`POST /auth/login`** (Limite Global IP) | `IP` de Origem | **50 requisições** | **1 hora** | Mitiga ataques distribuídos de *credential stuffing* (teste de listas de senhas vazadas em massa). |
| **`POST /auth/register`** | `IP` de Origem | **3 contas** | **1 hora** | Impede a criação automatizada em massa de contas por bots ou agentes maliciosos. |
| **`POST /auth/refresh`** | Chave Composta `(user_id + device_id)` | **20 requisições** | **1 minuto** | Permite rajadas naturais de reconexão do app mobile, bloqueando loops infinitos ou abuso da rota. |
| **`POST /auth/forgot-password`** | Chave Composta `(IP + email)` | **3 requisições** | **1 hora** | Previne spam e abuso de envio de e-mails com códigos OTP. |
| **`POST /auth/reset-password`** (Tentativas de Código) | Chave Composta `(IP + email)` | **3 falhas** | **15 minutos** | Bloqueia ataques de força bruta na adivinhação do código OTP de 6 dígitos. |

### 2.3 Contrato de Resposta e Cabeçalhos Padronizados (HTTP 429)

Quando qualquer um dos limites acima for ultrapassado:
1. **Código de Status HTTP:** `429 Too Many Requests`.
2. **Cabeçalhos HTTP Mandatórios (Padrão IETF RateLimit / RFC 6585):**
   - `RateLimit-Limit`: Quantidade máxima de requisições permitidas na janela (ex.: `5`).
   - `RateLimit-Remaining`: Quantidade de requisições restantes na janela atual (`0` quando bloqueado).
   - `RateLimit-Reset`: Tempo restante em segundos até a expiração total da janela de bloqueio (ex.: `742`).
   - `Retry-After`: Tempo obrigatório em segundos que o cliente DEVE aguardar antes de realizar nova tentativa (ex.: `742`).
3. **Payload JSON de Erro Padronizado:**
   ```json
   {
     "error": "RATE_LIMIT_EXCEEDED",
     "message": "Limite de tentativas excedido. Por favor, aguarde antes de tentar novamente.",
     "retryAfterSeconds": 742
   }
   ```

---

## Histórias de Usuário e Critérios de Aceite (BDD)

### P1: Acessar Busca no Mapa com Autenticação Obrigatória MVP

**História de Usuário**: Como usuário cadastrado e logado, quero acessar a visualização e busca no mapa para encontrar igrejas compatíveis por localização com segurança.

**Por que P1**: A busca no mapa é funcionalidade central e o produto definiu que exige usuário identificado.

#### Cenário 1: Busca no mapa bem-sucedida para usuário autenticado
- **GIVEN** que o usuário está autenticado e possui um Access Token JWT válido
- **WHEN** o usuário emite uma requisição de busca de igrejas no mapa
- **THEN** o sistema SHALL aceitar o token e retornar os resultados georreferenciados disponíveis.

#### Cenário 2: Bloqueio de acesso anônimo à busca no mapa
- **GIVEN** que o usuário não está autenticado (sem cabeçalho `Authorization`)
- **WHEN** o usuário tenta acessar os endpoints de busca do mapa
- **THEN** o sistema SHALL rejeitar a requisição com HTTP 401 Unauthorized
- **AND** orientar o cliente para a tela de login ou cadastro.

---

### P1: Criar Perfil de Usuário sem Autenticação Prévia (Onboarding) MVP

**História de Usuário**: Como novo visitante, quero criar meu perfil de usuário no meu primeiro contato com a aplicação sem precisar estar logado para começar a usar a plataforma.

**Por que P1**: É a porta de entrada para novos usuários que desbloqueia a autenticação e o uso dos recursos protegidos (como o mapa).

#### Cenário 3: Cadastro público de usuário com emissão do primeiro par de tokens e logs de auditoria
- **GIVEN** que o visitante não possui conta e envia dados válidos para cadastro contendo senha com no mínimo 8 caracteres (ao menos 1 letra e 1 número conforme AD-024)
- **WHEN** o sistema processa a criação do perfil de usuário
- **THEN** o sistema SHALL persistir a nova conta
- **AND** registrar o evento de cadastro na trilha de auditoria contendo obrigatoriamente `client_ip`, `client_port`, `timestamp_utc` (ISO 8601 UTC), `user_agent` e `verification_metadata` em repositório de log seguro append-only com retenção obrigatória de no mínimo 6 meses (180 dias) e expiração automatizada conforme Marco Civil (art. 15) e LGPD
- **AND** emitir imediatamente um par de tokens: um Access Token JWT (15 min) e um Refresh Token (60 dias) com novo `family_id`
- **AND** o cliente SHALL salvar o Refresh Token no hardware seguro (Keychain/Keystore).

---

### P1: Proteger Contribuições e Titularidade do Usuário MVP

**História de Usuário**: Como usuário cadastrado, quero que apenas eu possa alterar meu próprio perfil e que minhas ações sejam associadas à minha identidade para proteger meus dados contra adulterações de terceiros.

**Por que P1**: A integridade das preferências, dados pessoais e feedback exige uma fronteira rígida de autoria e propriedade.

#### Cenário 4: Alteração autorizada de perfil próprio pelo titular
- **GIVEN** que o usuário "Lucas" está autenticado com Access Token contendo seu `user_id`
- **WHEN** Lucas envia uma alteração em suas próprias preferências de perfil
- **THEN** o sistema SHALL autorizar e persistir as alterações.

#### Cenário 5: Bloqueio de alteração em perfil de terceiros (Ownership)
- **GIVEN** que o usuário "Lucas" está autenticado
- **WHEN** Lucas tenta enviar uma requisição de alteração no perfil do usuário "Mateus"
- **THEN** o sistema SHALL negar a operação com HTTP 403 Forbidden
- **AND** NÃO DEVE persistir nenhuma modificação.

---

### P1: Proteger a Representação da Igreja MVP

**História de Usuário**: Como representante verificado de uma igreja, quero exclusividade autorizada na gestão das informações da congregação para que apenas líderes credenciados possam alterar dados do perfil da igreja.

**Por que P1**: Integridade e segurança dos dados eclesiásticos, alinhado à funcionalidade de reivindicação (`church-profile-claim`).

#### Cenário 6: Edição autorizada por representante verificado
- **GIVEN** que o usuário possui status `Verified` para a congregação X (concedido via `church-profile-claim`)
- **WHEN** o representante envia alterações de horários de cultos da congregação X
- **THEN** o sistema SHALL aceitar e persistir as informações.

#### Cenário 7: Bloqueio de edição para usuário comum não verificado
- **GIVEN** que o usuário está autenticado, mas NÃO possui status verificado na congregação X
- **WHEN** o usuário tenta alterar dados do perfil da congregação X
- **THEN** o sistema SHALL rejeitar a requisição com HTTP 403 Forbidden.

---

### P1: Renovar Sessão Silenciosamente com Rotação de Tokens (RTR) e Sliding Expiration ⭐ MVP

**História de Usuário**: Como usuário frequente do app, quero que minha sessão seja renovada em segundo plano de forma contínua para que eu nunca seja deslogado enquanto estiver utilizando o produto periodicamente.

**Por que P1**: Padrão de mercado que elimina a fadiga de autenticação e mantém o engajamento contínuo.

#### Cenário 8: Renovação transparente com rotação de Refresh Token (RTR)
- **GIVEN** que o Access Token do usuário expirou (mais de 15 minutos de emissão)
- **AND** o usuário possui um Refresh Token ativo no Keychain/Keystore emitido na família `FAM-102`
- **WHEN** o app envia uma requisição para `POST /auth/refresh` com o Refresh Token e `deviceId`
- **THEN** o sistema SHALL validar o token, marcá-lo imediatamente como `Consumed`
- **AND** emitir um novo Access Token (15 minutos) e um novo Refresh Token vinculado à mesma família `FAM-102`
- **AND** aplicar Sliding Expiration, estendendo a validade do novo Refresh Token para +60 dias
- **AND** o app SHALL substituir o par antigo pelo novo par no armazenamento seguro.

#### Cenário 9: Request Queuing no cliente HTTP ao expirar Access Token
- **GIVEN** que o Access Token expirou enquanto o usuário navegava no aplicativo
- **AND** o app disparou 3 requisições em paralelo para carregar o mapa e detalhes
- **WHEN** a primeira requisição recebe resposta HTTP 401 Unauthorized
- **THEN** o interceptor do app SHALL reter em fila as 3 requisições em memória
- **AND** executar uma única chamada `POST /auth/refresh` em segundo plano
- **AND** ao receber o novo par de tokens, reexecutar as 3 requisições retidas com o novo Access Token
- **AND** o usuário SHALL visualizar as telas carregadas sem interrupção nem telas de login.

---

### P1: Detectar Reúso de Refresh Token e Revogar Família (Automatic Breach Detection) ⭐ MVP

**História de Usuário**: Como equipe de segurança, quero que o sistema detecte a apresentação de tokens já consumidos para invalidar imediatamente a sessão e impedir que invasores utilizem tokens roubados.

**Por que P1**: Requisito crítico de segurança em implementações de Refresh Token Rotation.

#### Cenário 10: Bloqueio de invasor e invalidação por reúso de token
- **GIVEN** que o Refresh Token `RT-AAA` da família `FAM-555` já foi consumido e substituído por `RT-BBB`
- **WHEN** uma requisição chega em `POST /auth/refresh` apresentando o token antigo `RT-AAA`
- **THEN** o sistema SHALL detectar a tentativa de reúso
- **AND** revogar imediatamente todos os tokens ativos vinculados à família `FAM-555`
- **AND** responder com HTTP 401 Unauthorized e código `TOKEN_BREACH_DETECTED`
- **AND** registrar o incidente na trilha de auditoria
- **AND** forçar o encerramento da sessão em todos os dispositivos que utilizavam a família `FAM-555`.

---

### P1: Encerrar Sessão por Critérios Estritos de Logout ⭐ MVP

**História de Usuário**: Como usuário, quero ser desconectado apenas quando eu mesmo solicitar, alterar minha senha ou após longo período de inatividade, para ter controle e previsibilidade sobre minha conta.

**Por que P1**: Garante que o logout ocorra apenas por razões legítimas de segurança e vontade do usuário.

#### Cenário 11: Logout manual voluntário
- **GIVEN** que o usuário está logado em seu smartphone
- **WHEN** o usuário toca em "Sair da Conta"
- **THEN** o app SHALL enviar requisição para `POST /auth/logout` com o Refresh Token
- **AND** o backend SHALL revogar imediatamente o Refresh Token no banco de dados
- **AND** o app SHALL apagar as credenciais do Keychain/Keystore e redirecionar para a tela inicial.

#### Cenário 12: Desconexão forçada por alteração de senha
- **GIVEN** que o usuário possui sessões ativas em seu smartphone e em um tablet
- **WHEN** o usuário redefine sua senha com sucesso
- **THEN** o backend SHALL revogar todas as famílias de Refresh Tokens ativas associadas àquele `user_id`
- **AND** qualquer tentativa posterior de renovação silenciosa nos dispositivos SHALL falhar com HTTP 401
- **AND** ambos os dispositivos SHALL redirecionar para a tela de login exigindo a nova senha.

#### Cenário 13: Desconexão por inatividade prolongada superior a 60 dias
- **GIVEN** que o usuário não abre o aplicativo há mais de 60 dias corridos
- **WHEN** o usuário abre o aplicativo e o app tenta renovar o token
- **THEN** o backend SHALL identificar que o Refresh Token expirou por decurso de prazo
- **AND** rejeitar a renovação com HTTP 401 Unauthorized
- **AND** o app SHALL limpar o armazenamento seguro e exibir a tela de login.

---

### P1: Proteger Endpoints de Autenticação com Rate Limiting Granular ⭐ MVP

**História de Usuário**: Como equipe de segurança, quero limitar tentativas de requisição nas rotas críticas de autenticação para mitigar ataques de força bruta, *credential stuffing* e criação abusiva de contas.

**Por que P1**: Proteção indispensável de resiliência e integridade das credenciais de acesso.

#### Cenário 14: Bloqueio de login após 5 falhas por IP e email com HTTP 429
- **GIVEN** que o cliente emitiu 5 tentativas de login incorretas consecutivas para a chave composta `IP: 189.10.20.30` + `email: usuario@email.com` nos últimos 15 minutos
- **WHEN** o cliente emite a 6ª tentativa de login para o mesmo e-mail a partir do mesmo IP
- **THEN** o sistema SHALL bloquear a requisição sem consultar a base de credenciais
- **AND** retornar HTTP 429 Too Many Requests
- **AND** incluir os headers `RateLimit-Limit: 5`, `RateLimit-Remaining: 0`, `RateLimit-Reset: <segundos>` e `Retry-After: <segundos>`
- **AND** retornar payload com `error: "RATE_LIMIT_EXCEEDED"`.

#### Cenário 15: Bloqueio global de IP por hora contra Credential Stuffing
- **GIVEN** que um endereço IP já realizou 50 tentativas de login (para diferentes e-mails) na última hora
- **WHEN** qualquer nova tentativa de login for enviada a partir desse IP
- **THEN** o sistema SHALL retornar imediatamente HTTP 429 Too Many Requests com header `Retry-After`.

#### Cenário 16: Bloqueio de criação abusiva de contas no registro
- **GIVEN** que um mesmo IP já registrou 3 novas contas de usuário na última hora
- **WHEN** uma 4ª requisição de criação de conta chegar em `POST /auth/register` a partir desse IP
- **THEN** o sistema SHALL rejeitar a requisição com HTTP 429 Too Many Requests
- **AND** informar no header `Retry-After` o tempo restante para liberação.

#### Cenário 17: Taxa suportada no refresh token para rajadas legítimas
- **GIVEN** que o app móvel disparou 5 requisições de refresh em 10 segundos durante abertura do app e transição de telas
- **WHEN** as requisições chegam para a chave `(user_id + device_id)`
- **THEN** o sistema SHALL processar as renovações normalmente pois estão abaixo do teto de 20 requisições por minuto.

---

### P2: Recuperar Acesso via Código OTP por E-mail ("Esqueci minha senha")

**História de Usuário**: Como usuário cadastrado que esqueceu a senha, quero receber um código numérico de verificação no meu e-mail para redefinir minha senha com segurança diretamente no aplicativo.

**Por que P2**: Funcionalidade essencial de autoatendimento que previne perda definitiva de acesso à conta, mantendo baixa fricção no mobile.

#### Cenário 18: Solicitação de código de recuperação por e-mail
- **GIVEN** que o usuário informa seu e-mail cadastrado na tela de recuperação de senha
- **WHEN** o cliente envia a requisição para `POST /auth/forgot-password`
- **THEN** o sistema SHALL gerar um código numérico OTP de 6 dígitos com validade de 15 minutos
- **AND** enviar o código para o e-mail cadastrado
- **AND** aplicar limite de taxa de no máximo 3 solicitações por hora por chave `(IP + email)`.

#### Cenário 19: Redefinição de senha com validação de OTP e revogação de sessões
- **GIVEN** que o usuário recebeu o código OTP de 6 dígitos em seu e-mail
- **WHEN** o usuário envia o código OTP acompanhado da nova senha (mínimo 8 caracteres, contendo ao menos 1 letra e 1 número conforme AD-024) para `POST /auth/reset-password`
- **THEN** o sistema SHALL validar o código dentro da janela de 15 minutos
- **AND** atualizar a senha criptografada do usuário
- **AND** revogar sumariamente todas as famílias de Refresh Tokens ativas associadas àquele `user_id` em todos os dispositivos (conforme Cenário 12 e AD-010)
- **AND** invalidar o código OTP imediatamente para impedir reúso.

---

## Casos de Borda

- **Concorrência de Refresh Tokens no Cliente:** Caso duas requisições paralelas móveis tentem renovar o token simultaneamente por falha de fila, o cliente HTTP com request queuing garante uma única chamada sequencial; no backend, uma janela de tolerância de 2 segundos pode ser implementada para aceitar a mesma requisição em trânsito se originada do mesmo IP e `device_id`.
- **Perda de Conexão durante o Refresh:** Se a rede falhar após o backend queimar o token antigo e antes do cliente receber o novo par, a próxima tentativa utilizará o token que o backend marcou como consumido. O cliente deve registrar o estado transitório e tentar reenvio idempotente ou, caso falhe com breach detection, solicitar reautenticação limpa ao usuário.
- **Falha de Inicialização do Armazenamento Seguro:** Se o Keystore/Keychain estiver corrompido ou inacessível no aparelho, a aplicação deve falhar de forma segura, informando indisponibilidade de sessão persistente sem salvar credenciais em texto plano.
- **Falha ou Timeout do Redis no Rate Limiting:** Em caso de indisponibilidade transitória do cluster Redis, a camada de API aplica *fail-open* controlado com log crítico de alerta para monitoramento, evitando que usuários legítimos fiquem bloqueados em caso de manutenção do cache.
- **Tratamento de Proxies e NAT (IP Real do Cliente):** O rate limiter deve extrair o IP real do cliente exclusivamente a partir do cabeçalho `X-Forwarded-For` confiável fornecido pelo API Gateway / Reverse Proxy configurado, evitando *spoofing* de IP e mitigando falsos positivos em redes compartilhadas (NAT).

---

## Dimensões de Requisitos Implícitos (Sweep)

| Dimensão | Cobertura na Especificação |
| -------- | -------------------------- |
| **Validação de Entrada e Limites** | Validação sintática de e-mail (RFC 5322); senha alfanumérica mínima de 8 caracteres (ao menos 1 letra e 1 número - AD-024); código OTP numérico de 6 dígitos com TTL de 15 minutos; validação de formato e unicidade de `deviceId`; verificação da assinatura criptográfica e integridade de claims do JWT (`sub`, `exp`, `iat`, `jti`, `family_id`); sanitização de payloads de autenticação. |
| **Estados de Falha e Parciais** | Respostas de erro padronizadas: HTTP 401 para credenciais incorretas, tokens expirados ou violação (`TOKEN_BREACH_DETECTED`); HTTP 403 para violação de titularidade (*ownership*) ou falta de status verificado; HTTP 429 para rate limit; comportamento de *fail-open* controlado com log crítico em caso de falha transitória do cluster Redis; resiliência a oscilações de rede no refresh através de enfileiramento no cliente móvel. |
| **Idempotência, Deduplicação e Precedência** | Operação `POST /auth/logout` idempotente; chamada única sequencial de refresh pelo interceptor HTTP com *request queuing* no app, eliminando requisições concorrentes duplicadas; tolerância de até 2 segundos em trânsito no backend para chamadas com mesmo IP e `device_id`. |
| **Fronteiras de Autenticação e Rate Limits** | Rotas públicas estritas a cadastro (`/auth/register`), login (`/auth/login`) e recuperação (`/auth/forgot-password`, `/auth/reset-password`); busca no mapa e rotas protegidas exigem JWT válido (AD-007); proteção com *Sliding Window Counter* no Redis (`POST /auth/login`: 5 falhas/15m por IP+email e 50 req/h global por IP; `POST /auth/register`: 3 contas/h por IP; `POST /auth/refresh`: 20 req/min por user_id+device_id; `POST /auth/forgot-password`: 3 req/h por IP+email; `POST /auth/reset-password`: 3 falhas/15m por IP+email), retornando HTTP 429 com cabeçalhos IETF (AD-011 e AD-024). |
| **Concorrência e Ordenação** | Transações atômicas no banco relacional para queima do Refresh Token (`Consumed`) e geração do novo par (`Active`) na mesma transação; revogação em lote atômica de toda a família de tokens sob detecção de violação; execução atômica de contadores via scripts Lua no Redis sem condições de corrida. |
| **Ciclo de Vida de Dados e Expiração** | Access Token JWT com validade estrita de curta duração (15 minutos); Refresh Token com validade de 60 dias corridos sob *Sliding Expiration*; expiração natural pós-inatividade (60 dias); revogação imediata em logout manual ou troca de senha com expiração natural de Access Token residual sem denylist (AD-024); retenção obrigatória de logs de auditoria por no mínimo 6 meses (180 dias) em modo *append-only* (Marco Civil art. 15 e LGPD - AD-014). |
| **Observabilidade e Auditoria** | Gravação padronizada em repositório de log seguro dos campos `client_ip` (extraído com segurança de `X-Forwarded-For`), `client_port`, `timestamp_utc` (ISO 8601 UTC), `user_agent` e `verification_metadata` para eventos de cadastro, login, logout, renovação de token, recuperação de senha, detecção de violação e bloqueio por rate limiting. |
| **Segurança de Hardware e Armazenamento Local** | Exigência mandatória de armazenamento seguro de Refresh Tokens exclusivamente no iOS Keychain e Android Keystore / EncryptedSharedPreferences (AD-010); proibição estrita de persistência em texto plano ou locais desprotegidos. |

---

## Rastreabilidade de Requisitos

| ID do Requisito | História / Escopo Detalhado | Fase | Status |
| --------------- | --------------------------- | ---- | ------ |
| AUTH-01 | P1: Acesso Obrigatório à Busca no Mapa com Autenticação JWT (AD-007) | Specify | Confirmado |
| AUTH-02 | P1: Cadastro Público de Usuário e Logs de Auditoria do Marco Civil (AD-008 e AD-014) | Specify | Confirmado |
| AUTH-03 | P1: Proteção de Contribuições e Titularidade do Usuário / Ownership (AD-008) | Specify | Confirmado |
| AUTH-04 | P1: Proteção Exclusiva da Representação da Igreja via Claim Verificado (AD-009) | Specify | Confirmado |
| AUTH-05 | P1: Renovação Silenciosa de Sessão com Rotação (RTR) e Sliding Expiration 60d (AD-010) | Specify | Confirmado |
| AUTH-06 | P1: Detecção Automática de Reúso e Revogação Imediata de Família / Anti-Roubo (AD-010) | Specify | Confirmado |
| AUTH-07 | P1: Interceptor HTTP com Request Queuing e Armazenamento Seguro em Hardware Mobile (AD-010) | Specify | Confirmado |
| AUTH-08 | P1: Critérios Determinísticos de Logout (Inatividade 60d, Manual e Troca de Senha) (AD-010 e AD-024) | Specify | Confirmado |
| AUTH-09 | P1: Rate Limiting Granular com Sliding Window Counter no Redis e HTTP 429 IETF (AD-011) | Specify | Confirmado |
| AUTH-10 | P2: Recuperação de Acesso via Código OTP de 6 Dígitos por E-mail (AD-024) | Specify | Confirmado |

**Cobertura:** 10 requisitos estruturados, 10 confirmados com critérios BDD e decisões arquiteturais vinculadas, 0 pendentes de especificação. Prontos para Design.

---

## Critérios de Sucesso

- [x] Acesso à busca por mapa é protegido e restrito a usuários autenticados com JWT válido.
- [x] Visitantes conseguem realizar o cadastro inicial de perfil sem barreiras de autenticação prévia, com senha mínima de 8 caracteres alfanuméricos.
- [x] Escritas em perfis e congregações obedecem estritamente às regras de titularidade (ownership) e representação verificada.
- [x] Usuários ativos no aplicativo permanecem logados indefinidamente através da expiração deslizante de 60 dias, sem interrupção de uso.
- [x] 100% das renovações de sessão utilizam Rotação de Refresh Token (uso único).
- [x] Qualquer tentativa de reúso de refresh token consumido invalida instantaneamente toda a família de tokens (`family_id`).
- [x] Nenhum token de sessão é armazenado em texto plano no dispositivo móvel.
- [x] 100% das tentativas abusivas nos endpoints de login, registro, refresh e recuperação de senha são contidas com resposta HTTP 429, payload explicativo e cabeçalhos `RateLimit-Limit`, `RateLimit-Remaining`, `RateLimit-Reset` e `Retry-After`.
- [x] Usuários conseguem recuperar o acesso através de código OTP de 6 dígitos enviado por e-mail com TTL de 15 minutos, provocando a revogação de todas as sessões ativas nos aparelhos ao redefinir a senha.