# Especificação de Reivindicação de Perfil de Igreja (Claim)

## Problema

Milhares de igrejas existem no mapa e na base de dados de descoberta sem um gestor oficial vinculado (`Unclaimed`). Para transformar essas entidades em perfis ativos e confiáveis no produto, é necessário permitir que líderes e representantes comprovem sua legitimidade e assumam a gestão do perfil da igreja. Sem um mecanismo seguro de reivindicação, validação de vínculos, trilha de auditoria e resolução de disputas, o sistema fica vulnerável a fraudes, apropriação indevida de perfis comunitários, exposição a riscos jurídicos e desinformação.

## Objetivos

- [ ] Permitir que representantes legítimos iniciem e concluam a reivindicação de igrejas exibidas na plataforma.
- [ ] Implementar ciclo de vida rigoroso com estados `Unclaimed`, `Pending_Verification`, `Verified` e `In_Dispute`.
- [ ] Adotar **Hierarquia Probatória Estrita em 3 Níveis** (Nível 1: Cartório RCPJ/CNPJ; Nível 2: Domínio/Canais Institucionais; Nível 3: Sociais/Presenciais).
- [ ] Definir matriz de permissões segregando o acesso a dados operacionais básicos (Nível 3 e 2) de recursos críticos e financeiros/PIX (exclusivo Nível 1).
- [ ] Exigir aceite explícito de Termos de Uso com enquadramento legal como Provedora de Aplicação (Lei 12.965/2014), declaração de mera intermediária técnica nos Níveis 2 e 3, declaração sob as penas do art. 299 CP e vinculação funcional ao mecanismo ativo de Notice and Takedown.
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
| Isenção de responsabilidade da plataforma | **Enquadramento Legal**: Provedora de Aplicação de Internet (Lei nº 12.965/2014 - Marco Civil da Internet). **Termos de Uso**: cláusula declaratória obrigatória de que a plataforma atua como mera intermediária técnica nos fluxos de validação simplificada (Níveis 2 e 3). **Mecanismo Ativo de Takedown**: isenção funcionalmente atrelada à operação contínua de "Denúncia / Notificação e Retirada", assegurando congelamento imediato ou transferência sumária perante provocação do representante legal legítimo com documento de Nível 1. | Conformidade com os arts. 18 e 19 da Lei nº 12.965/2014, segurança jurídica contra responsabilidade civil subsidiária e preservação da agilidade cadastral comunitária. | **Sim** |
| Retenção de logs do Marco Civil | Coleta padronizada e obrigatória de registros de conexão de aplicação nos eventos de claim e cadastro contendo: `client_ip` (IPv4/IPv6), `client_port` (porta lógica de origem), `timestamp_utc` (data/hora precisa em ISO 8601 no fuso UTC), `user_agent` (identificador do browser/dispositivo/app) e `verification_metadata` (dados complementares da prova: telefone com hash, token de bio, id de sessão social, hashes documentais). Armazenamento em repositório seguro com acesso restrito e proteção contra edição/exclusão (modo *append-only*), com período de retenção obrigatório de no mínimo 6 meses (180 dias) e expiração/arquivamento automatizado para conformidade com a LGPD. | Cumprimento estrito do art. 15 da Lei Federal nº 12.965/2014 (Marco Civil da Internet) e aderência ao princípio da limitação de armazenamento e necessidade da LGPD (Lei nº 13.709/2018). | **Sim** |
| Tempo de expiração de reivindicação pendente | **Políticas de Timeout / TTL Diferenciadas**: **Fluxo Documental** com prazo máximo de **7 dias corridos** para upload de atas/comprovantes antes da anulação automática; **Fluxo Social/Digital** com prazo reduzido de **48 horas** para inserção do código temporário na bio/redes sociais antes do descarte da solicitação. **Comportamento de Transição**: ao atingir o timeout sem submissão de evidências, o sistema executa a transição automática `Pending_Verification` → `Unclaimed`, revogando todas as permissões temporárias/rascunhos e liberando o perfil imediatamente para nova reivindicação. **Notificação**: envio de lembrete com antecedência de **24 horas** informando o prazo final para submissão das provas antes do cancelamento do claim. | Impede o bloqueio indefinido ou especulativo de perfis de igrejas, assegura agilidade na liberação de entidades para a comunidade, estabelece tempo razoável para obtenção de documentos cartoriais e notifica preventivamente o solicitante. | **Sim** |
| Prazo de resposta em contestação paritária (`In_Dispute`) | **Janela Operacional de Contraditório**: prazo improrrogável de **5 dias úteis** a partir da abertura do incidente para apresentação de Certidão de Breve Relato ou Inteiro Teor atualizada do RCPJ. **Regras em Conflito**: dados públicos da igreja mantidos visíveis no mapa para consulta comunitária; edições de perfil, permissões de membros e dados de arrecadação/PIX bloqueados imediatamente para ambos os litigantes (com aviso público de disputa). **Resolução**: **Prevalência Registral** (certidão com averbação mais recente assume `Verified` Nível 1); **Inércia** (desclassificação sumária da parte que não anexar o documento no prazo); **Litígio Irresolvível** (persistindo dúvida jurídica insanável pela moderação técnica, a reivindicação é anulada, retornando a `Unclaimed` com orientação para resolução em via judicial). | Garante contraditório célere sem paralisar o perfil público no mapa, mitiga riscos de apropriação indevida ou fraude financeira durante o litígio, baseia a solução na fé pública dos registros públicos e estabelece fronteira clara de atuação perante disputas jurídicas complexas. | **Sim** |
| Revogação sumária de Nível 2 e Nível 3 | Contestação válida com documento de Nível 1 revoga sumariamente e imediatamente os acessos de titulares de Nível 2 ou 3, sem exigência de aguardar inércia ou prazo de defesa prévia. | Prevalência incontestável da fé pública e registros públicos cartoriais. | **Sim** |
| Raio de tolerância para geofencing | **Parâmetros de Geofence e Precisão**: raio de tolerância de **100 metros** a partir das coordenadas centrais oficiais cadastradas da igreja; leitura do GPS deve informar precisão horizontal (`accuracy`) **≤ 50 metros**. **Processamento Seguro**: cálculo de distância processado **exclusivamente pelo backend** (fórmula de Haversine ou extensão espacial de banco de dados). **Segurança do Cliente**: cliente mobile verifica ativamente e recusa localizações simuladas (`mock locations` ativadas em opções de desenvolvedor). **Tratamento de Exceção**: distância > 100 metros retorna erro explícito padronizado `FORA_DO_RAIO_PERMITIDO`. | Compensa imprecisões transitórias de GPS móvel em áreas urbanas sem comprometer a comprovação de presença física real, garante confiabilidade do cálculo server-side imune a manipulações do cliente e neutraliza fraudes de localização simulada. | **Sim** |

**Questões em aberto:** nenhuma. Todas as 7 hipóteses críticas da especificação de reivindicação (`church-profile-claim`) estão integralmente confirmadas pelo responsável do produto.

---

## 1. Ciclo de Vida e Máquina de Estados

### Diagrama de Estados e Transições

```
  ┌────────────────────────────────────────────────────────────────────────┐
  │                                                                        │
  ▼                                                                        │
┌───────────┐      Início de Claim + ToS      ┌──────────────────────┐     │ Timeout TTL (7d doc / 48h social)
│ Unclaimed │ ──────────────────────────────> │ Pending_Verification │ ────┼─ou Reprovação Total (3x)
└───────────┘                                 └──────────────────────┘     │ [Lembrete enviado 24h antes]
      ▲                                                   │                │
      │                                                   │ Aprovação      │
      │                                                   ▼ de Provas      │
      │                                             ┌───────────┐          │
      │   Fraude mútua /                            │           │ <────────┼──────────┐
      │   Revogação legal                           │ Verified  │          │          │ Contestação
      │                                             └───────────┘          │          │ improcedente
      │                                                   │                │          │
      │                                                   │ Contestar      │          │
      │                                                   ▼ Propriedade    │          │
      │                                             ┌───────────┐          │          │
      └──────────────────────────────────────────── │In_Dispute │ ─────────┴──────────┘
                                                    └───────────┘
                                                          │ Descredenciamento Sumário /
                                                          │ Transferência de Titularidade
                                                          └───────────────────────────────────────────────┘
```

### Tabela de Transições de Estado

| Estado Atual | Evento / Condição | Próximo Estado | Efeito no Sistema |
| ------------ | ----------------- | -------------- | ----------------- |
| `Unclaimed` | Solicitante autenticado aceita ToS sob art. 299 CP, registra logs e submete método de validação inicial | `Pending_Verification` | Bloqueia novas reivindicações simples concorrentes; concede acesso preliminar em modo rascunho. |
| `Pending_Verification` | Provas submetidas atingem aprovação exigida (Nível 1, Nível 2 ou Nível 3) | `Verified` | Concede perfil correspondente ao nível validado; ativa selo da igreja; notifica o representante. |
| `Pending_Verification` | Timeout de TTL expirado sem submissão de evidências (7 dias corridos para fluxo documental ou 48 horas para fluxo social/digital), ou 3 reprovações consecutivas | `Unclaimed` | **Expiração Automática:** Revoga todas as permissões temporárias/rascunhos, descarta dados não verificados e torna o perfil imediatamente elegível para nova reivindicação (lembrete preventivo disparado 24h antes). |
| `Verified` (Nível 2 ou 3) | Requerente submete contestação acompanhada de documentação válida de Nível 1 | `Verified` (Nível 1) | **Resolução Automática de Disputa:** revoga sumariamente o vínculo anterior de Nível 2 ou 3 e transfere posse ao requerente de Nível 1; notifica titular anterior por prevalência documental legal, sem direito a bloqueio unilateral. |
| `Verified` (Nível 1) | Terceiro apresenta contestação documental também de Nível 1 (mandatos/atas concorrentes) | `In_Dispute` | **Instauração de Conflito Paritário:** Mantém dados públicos visíveis no mapa para consulta; congela imediatamente edições de perfil, permissões de membros e arrecadação/PIX para ambos; abre janela de contraditório de 5 dias úteis para certidão do RCPJ. |
| `In_Dispute` | Prevalência Registral (certidão com averbação mais recente no RCPJ) OU desclassificação sumária da outra parte por inércia após 5 dias úteis | `Verified` (Nível 1) | Restaura plenos poderes de gestão à parte vencedora, remove congelamento e encerra disputa. |
| `In_Dispute` | Litígio Irresolvível (dúvida jurídica insanável pela moderação técnica), inércia mútua, fraude mútua ou congregação desativada | `Unclaimed` | Anula sumariamente ambas as reivindicações, revoga acessos de ambos, restaura a entidade ao estado neutro e orienta as partes para resolução na via judicial. |

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
  - **Parâmetros do Geofence:** Raio de validação fixado em até **100 metros** a partir das coordenadas centrais registradas da igreja.
  - **Precisão Mínima do Dispositivo:** A leitura do GPS DEVE informar precisão horizontal (`accuracy`) **≤ 50 metros** para ser considerada confiável. Leituras com erro de dispersão superior a 50 metros são rejeitadas com erro explicativo (`PRECISAO_GPS_INSUFICIENTE`).
  - **Processamento Server-Side Obrigatório:** O cálculo da distância geodésica entre o usuário e o templo DEVE ser processado exclusivamente pelo backend (via fórmula de Haversine ou extensão espacial de banco de dados, como PostGIS `ST_DistanceSphere`). É terminantemente vedado o aceite de cálculos ou flags de proximidade gerados no cliente.
  - **Segurança Anti-Fraude e Mock Locations:** O cliente mobile DEVE verificar ativamente as APIs de integridade do sistema operacional e recusar o envio caso localizações simuladas estejam ativas (`mock locations` ativadas em opções de desenvolvedor ou execução sob emuladores), bloqueando o fluxo com código `LOCALIZACAO_SIMULADA_DETECTADA`.
  - **Captura Fotográfica em Tempo Real:** Captura obrigatória de foto ao vivo pela câmera nativa da aplicação (bloqueado upload de arquivos da galeria), com enquadramento da fachada identificada ou púlpito/nave do templo.
  - **Tratamento de Exceção:** Caso a distância calculada pelo backend seja > 100 metros, o sistema SHALL retornar erro padronizado explicativo: `FORA_DO_RAIO_PERMITIDO`, informando que o solicitante precisa estar fisicamente no templo para validar via presença.
- **Método 3.B - Vínculo em Redes Sociais Oficiais:**
  - Geração de token temporário alfanumérico único (`SAC-XXXX-VERIFY`, validade de **48 horas**).
  - Inserção do token na bio/descrição da conta pública da igreja no Instagram, Facebook ou canal oficial no YouTube dentro do prazo fatal de 48 horas do fluxo social/digital.

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

### 4.1 Termos de Uso, Enquadramento Legal e Regime de Responsabilidade (ToS)
Para iniciar qualquer solicitação de reivindicação de perfil, o usuário autenticado DEVE obrigatoriamente assinar digitalmente/aceitar os Termos de Uso específicos de reivindicação:
1. **Declaração de Legitimidade sob as Penas da Lei:** O solicitante declara expressamente:
   > *"Declaro, sob as penas da lei e em conformidade com o art. 299 do Código Penal Brasileiro (Falsidade Ideológica), que possuo plenos poderes de representação e legitimidade legal ou eclesiástica para atuar em nome desta congregação religiosa."*
2. **Enquadramento Legal e Cláusula de Mera Intermediária Técnica (Lei 12.965/2014):**
   - A plataforma é formalmente enquadrada como **Provedora de Aplicação de Internet**, regida pela Lei Federal nº 12.965/2014 (Marco Civil da Internet, arts. 5º, VII, 18 e 19).
   - Nos fluxos de reivindicação simplificada (**Níveis 2 e 3**), os Termos de Uso contêm **cláusula declaratória obrigatória** em que o solicitante reconhece que a plataforma opera como **mera intermediária técnica neutra**, viabilizando a ponte entre a comunidade e a congregação, sem realizar auditoria presencial prévia nem atestar a veracidade imediata de dados operacionais inseridos por terceiros.
3. **Mecanismo Ativo de Takedown como Condição Funcional de Isenção (Safe Harbor):**
   - O regime de isenção de responsabilidade civil da plataforma é **funcionalmente atrelado à existência, eficácia e disponibilidade ininterrupta do mecanismo de "Denúncia / Notificação e Retirada" (*Notice and Takedown*)**.
   - A plataforma assegura a capacidade funcional de congelar imediatamente o perfil contestado (`In_Dispute`) ou transferir/revogar sumariamente vínculos irregulares no momento em que for formalmente provocada pelo representante legal portando documentação cartorial de Nível 1.
4. **Bloqueio de Continuidade:** O sistema DEVE rejeitar qualquer prosseguimento no fluxo caso os checkboxes de aceite da declaração do art. 299 CP e da cláusula de enquadramento técnico/ToS não sejam marcados.

### 4.2 Trilha de Auditoria e Coleta Obrigatória de Logs (Marco Civil da Internet)
Em observância ao **art. 15 da Lei Federal nº 12.965/2014 (Marco Civil da Internet)** e às diretrizes da **Lei nº 13.709/2018 (LGPD)**, para cada evento do ciclo de claim (início, aceite de ToS, submissão de prova, aprovação, contestação e alteração de estado) e cadastro de perfil, o sistema DEVE coletar e persistir de forma inviolável os seguintes registros de conexão de aplicação:

1. **Campos Obrigatórios de Auditoria no Evento de Claim/Cadastro:**
   - `client_ip`: Endereço IP do solicitante, com suporte obrigatório a IPv4 e IPv6 público (extraído com segurança de cabeçalho confiável de proxy reverso / API Gateway).
   - `client_port`: Porta lógica de origem da conexão cliente-servidor utilizada na requisição.
   - `timestamp_utc`: Data e hora precisa da transação em formato padronizado **ISO 8601** no fuso horário UTC (com resolução mínima de milissegundos).
   - `user_agent`: String completa de identificação de browser, sistema operacional, dispositivo e versão do aplicativo.
   - `verification_metadata`: Objeto estruturado com os dados complementares da prova fornecida, conforme o método e nível probatório adotado:
     - Hash criptográfico (SHA-256) do número de telefone celular validado via OTP;
     - Token alfanumérico gerado para bio de rede social e link/identificador da conta consultada;
     - ID de sessão de autenticação social ou canal proprietário;
     - Endereço de e-mail institucional validado via token;
     - Hashes criptográficos (SHA-256) dos documentos cartorários enviados (Ata de Posse / Estatuto);
     - Identificador da versão integral dos Termos de Uso (ToS) aceitos e número de CNPJ/CPF consultados.

2. **Política de Armazenamento e Retenção:**
   - **Armazenamento Seguro e Append-Only:** Os registros de auditoria DEVEM ser armazenados em repositório de log dedicado e seguro, com acesso estrito baseado no princípio do privilégio mínimo (least privilege), segregação de ambiente e proteção física e lógica contra edição, adulteração ou exclusão (operação exclusivamente em modo *append-only* / WORM - Write Once, Read Many).
   - **Período de Retenção Obrigatório:** Período de retenção obrigatório de no mínimo **6 meses (180 dias)**, conforme estipulado expressamente pelo art. 15 da Lei Federal nº 12.965/2014 (Marco Civil da Internet), sob sigilo e acessíveis exclusivamente para atendimento a ordens judiciais específicas ou auditorias internas formais de incidentes de segurança.
   - **Expiração e Arquivamento Automatizado (Conformidade com a LGPD):** Decorrido o prazo regulamentar obrigatório de 180 dias, o sistema DEVE executar rotina automatizada de expiração/arquivamento a frio ou expurgo/anonimização irreversível dos logs, prevenindo a retenção perene ou desnecessária de dados pessoais e registros de conexão, em estrita conformidade com o princípio da limitação do armazenamento da Lei Geral de Proteção de Dados (Lei nº 13.709/2018 - LGPD).

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

Quando uma contestação documental de Nível 1 for instaurada contra um perfil que já possuía credenciamento de Nível 1 (disputa de mandatos, cisões ou atas de assembleia concorrentes):

1. **Abertura do Incidente e Transição de Estado:**
   - O perfil da congregação transita imediatamente para o estado `In_Dispute`.
   - É exibido aviso informativo público no perfil: *"Perfil em processo de verificação de titularidade"*.

2. **Janela Operacional de Contraditório:**
   - O sistema estipula prazo improrrogável de **5 dias úteis** a partir da abertura do incidente para apresentação de **Certidão de Breve Relato ou Inteiro Teor atualizada do RCPJ** (Cartório de Registro Civil de Pessoas Jurídicas), atestando a vigência e tempestividade do mandato da diretoria.
   - Notificação formal automática é enviada simultaneamente a ambos os litigantes via e-mail e push, registrando o marco temporal inicial e a data/hora limite exata.

3. **Regras de Comportamento Durante o Conflito:**
   - **Visibilidade Pública Preservada:** Os dados públicos da igreja (localização geográfica, fotos aprovadas e horários regulares de cultos) permanecem **visíveis no mapa e na busca** para livre consulta e orientação da comunidade local.
   - **Bloqueio Imediato de Edições:** Fica bloqueada sumariamente para ambos os litigantes qualquer modificação de informações do perfil (horários, fotos, telefones, descrição e endereço).
   - **Bloqueio de Permissões e Membros:** Fica congelada qualquer alteração de administradores, convite de novos gestores ou revogação de acessos de equipe.
   - **Bloqueio Financeiro e Arrecadação:** São imediatamente desativadas e bloqueadas todas as opções de cadastro/alteração de chaves PIX, dados bancários e campanhas de doação, neutralizando riscos de desvios patrimoniais durante o litígio.

4. **Resolução e Transições Finais de Estado:**
   - **Prevalência Registral:** A parte que apresentar a certidão de averbação mais recente no RCPJ assume a titularidade legítima do perfil com status `Verified` (Nível 1). As permissões plenas de gestão são desbloqueadas e o aviso de disputa é removido.
   - **Desclassificação por Inércia:** A parte que não anexar o documento comprobatório dentro do prazo de 5 dias úteis é **desclassificada sumariamente**. O perfil é atribuído imediatamente à parte diligente que cumpriu a exigência documental.
   - **Litígio Irresolvível (Saída Judicial):** Persistindo dúvida jurídica insanável pela moderação técnica da plataforma (ex.: certidões conflitantes de serventias distintas, duplicidade registral com medidas judiciais liminares contraditórias ou fraude mútua), a reivindicação é **anulada de ofício**, revogando o acesso de ambos os litigantes. O status da igreja retorna para `Unclaimed` e o sistema emite notificação orientando formalmente as partes a resolverem a disputa na via judicial competente. A plataforma só reabrirá o perfil mediante determinação judicial formal com trânsito em julgado ou certidão de cancelamento averbada no RCPJ.

---

## 6. Casos de Borda e Tratamento de Falhas

- **Solicitações Simultâneas / Concorrentes:** O primeiro claim com ToS válido coloca a entidade em `Pending_Verification`. Tentativas concorrentes são bloqueadas, salvo se o segundo requerente apresentar prova de nível hierárquico superior (ex.: Nível 1 contra processo Nível 2 ou 3), caso em que a submissão de maior autoridade ganha precedência imediata e cancela o processo concorrente inferior.
- **Inércia em Análise Pendente (Políticas de Timeout / TTL):**
  - **Fluxo Documental:** Prazo máximo de **7 dias corridos** para upload de atas/comprovantes antes da anulação automática.
  - **Fluxo Social/Digital:** Prazo reduzido de **48 horas** para inserção do código temporário na bio/redes sociais antes do descarte da solicitação.
  - **Comunicação / Notificação Preventiva:** Envio obrigatório de lembrete com antecedência de **24 horas** informando o prazo final para submissão das provas antes do cancelamento do claim.
  - **Comportamento de Transição de Estado:** Ao atingir o timeout sem submissão de evidências, o sistema executa a transição automática: `Pending_Verification` → `Unclaimed`. Todas as permissões temporárias/rascunhos são sumariamente revogadas e o perfil fica imediatamente elegível para uma nova reivindicação por qualquer usuário.
- **Fraude Mútua ou Templo Extinto:** Caso a análise de disputa identifique apresentação de atas falsificadas por ambas as partes ou comprove que o templo encerrou atividades no local, a igreja é desvinculada de ambos os usuários e mantida como `Unclaimed` ou desativada.
- **Instabilidade em Serviços Externos de Auditoria:** Se a API de consulta pública de CNPJ ou o serviço de registro de logs sofrer lentidão ou timeout, a requisição transita para fila assíncrona garantida de persistência e NÃO rejeita o usuário sumariamente.

---

## Histórias de Usuário e Critérios de Aceite (BDD)

### P1: Aceite de ToS e Coleta de Logs de Auditoria ⭐ MVP

**História de Usuário**: Como representante legítimo, quero assinar os termos de responsabilidade sob as penas da lei ao reivindicar a igreja para garantir segurança e transparência jurídica no processo.

**Por que P1**: Requisito mandatório de compliance legal, conformidade com o Marco Civil da Internet e proteção civil da plataforma.

#### Cenário 1: Aceite explícito de ToS com enquadramento legal, cláusula de intermediária técnica e logs (Marco Civil)
- **GIVEN** que o usuário "Pastor André" está autenticado com IP público "200.180.10.5" e porta "44321"
- **AND** a igreja "Igreja Bíblica Central" está com status `Unclaimed`
- **WHEN** o Pastor André inicia o processo de claim sob fluxo simplificado (Nível 2 ou Nível 3)
- **AND** marca a caixa de seleção da declaração: *"Declaro, sob as penas da lei (art. 299 CP), ter poderes de representação..."*
- **AND** marca o aceite da cláusula declaratória reconhecendo a plataforma como Provedora de Aplicação de Internet e mera intermediária técnica
- **AND** clica em "Prosseguir com a Reivindicação"
- **THEN** o sistema SHALL registrar o evento na trilha de auditoria contendo `client_ip` ("200.180.10.5"), `client_port` ("44321"), `timestamp_utc` (formato ISO 8601 UTC), `user_agent` e `verification_metadata` (versão integral do ToS aceito)
- **AND** persistir os registros em repositório de logs seguro em modo append-only com retenção obrigatória de no mínimo 6 meses (180 dias)
- **AND** aplicar rotina de expiração/arquivamento automatizado após o prazo regulamentar para conformidade com a LGPD
- **AND** atrelar a validação simplificada ao mecanismo ativo e contínuo de Takedown
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

#### Cenário 6: Reivindicação via Presença Física (Geofencing ≤ 100m, Accuracy ≤ 50m e Foto ao vivo)
- **GIVEN** que o solicitante está fisicamente nas imediações do templo da igreja
- **AND** a leitura de GPS apresenta precisão horizontal de 15 metros (`accuracy` = 15 ≤ 50m)
- **AND** o cliente mobile confirma que não há localização simulada ativa (`mock location` = false)
- **WHEN** o backend processa o cálculo geodésico da distância via fórmula de Haversine e obtém 45 metros (≤ 100 metros)
- **AND** o solicitante captura foto da fachada em tempo real pela câmera do aplicativo
- **THEN** o sistema SHALL aceitar as coordenadas e armazenar a imagem com carimbo temporal e logs
- **AND** aprovar a validação como Nível 3 (status `Verified`).

#### Cenário 7: Rejeição de presença física por distância fora do geofence (> 100 metros)
- **GIVEN** que o solicitante envia coordenadas com GPS autêntico e precisão horizontal de 20 metros
- **WHEN** o backend processa o cálculo da distância via fórmula de Haversine e constata 250 metros em relação às coordenadas registradas da igreja
- **THEN** o sistema SHALL rejeitar a validação
- **AND** retornar erro com código padronizado `FORA_DO_RAIO_PERMITIDO`
- **AND** exibir mensagem explicativa indicando que o solicitante precisa estar fisicamente no templo (raio máximo de 100 metros) para validar via presença
- **AND** NÃO DEVE alterar o status da congregação.

#### Cenário 8: Rejeição de presença física por detecção de localização simulada (Mock Location)
- **GIVEN** que o dispositivo móvel do usuário está com opções de desenvolvedor ativadas e aplicativo de simulação de GPS em execução
- **WHEN** o usuário tenta submeter a validação por presença física
- **THEN** o cliente mobile SHALL verificar a API de localização do sistema operacional e detectar a ativação de `mock location`
- **AND** bloquear sumariamente a requisição antes do envio com código `LOCALIZACAO_SIMULADA_DETECTADA`
- **AND** impedir a captura de imagem e a validação presencial.

#### Cenário 9: Rejeição de presença física por baixa precisão do sinal de GPS (Accuracy > 50 metros)
- **GIVEN** que o solicitante está sob sinal de satélite degradado gerando leitura com dispersão horizontal de 90 metros (`accuracy` = 90 > 50m)
- **WHEN** os metadados de localização são avaliados
- **THEN** o sistema SHALL recusar a leitura por confiabilidade insuficiente
- **AND** retornar erro `PRECISAO_GPS_INSUFICIENTE` solicitando que o usuário aguarde melhor fixação do sinal de satélite
- **AND** NÃO DEVE computar a tentativa de validação nem alterar o status da congregação.

---

### P1: Políticas de Timeout, Expiração e Lembretes de Reivindicação Pendente ⭐ MVP

**História de Usuário**: Como administrador da plataforma e usuário da comunidade, quero que reivindicações sem submissão de evidências expirem automaticamente conforme políticas de timeout diferenciadas (com lembrete preventivo de 24 horas), para garantir que perfis não fiquem bloqueados indefinidamente por solicitações abandonadas.

**Por que P1**: Garante fluidez e disponibilidade dos perfis para novas reivindicações legítimas, evitando bloqueios indevidos e retenção especulativa da igreja.

#### Cenário 10: Envio de lembrete com antecedência de 24 horas antes do timeout
- **GIVEN** que o usuário iniciou uma reivindicação para a igreja e o perfil está em `Pending_Verification`
- **AND** restam exatamente 24 horas para o encerramento do prazo de submissão de evidências (no fluxo documental de 7 dias ou social/digital de 48 horas)
- **WHEN** o serviço programado de verificação de TTL avalia a solicitação pendente
- **THEN** o sistema SHALL disparar notificação preventiva de alerta (push e e-mail) ao solicitante
- **AND** informar que restam 24 horas para a submissão das provas antes do cancelamento sumário do claim
- **AND** manter o perfil no estado `Pending_Verification`.

#### Cenário 11: Expiração automática de reivindicação por timeout no Fluxo Documental (7 dias)
- **GIVEN** que o solicitante iniciou o claim via Fluxo Documental (Nível 1) há 7 dias corridos
- **AND** nenhuma ata ou documento comprobatório foi submetido durante esse período
- **WHEN** o prazo limite de 7 dias corridos expira
- **THEN** o sistema SHALL executar a transição automática da congregação de `Pending_Verification` para `Unclaimed`
- **AND** revogar integralmente quaisquer permissões temporárias concedidas em modo rascunho
- **AND** descartar os dados preliminares não verificados
- **AND** tornar o perfil da congregação imediatamente elegível para nova reivindicação por qualquer usuário
- **AND** notificar o solicitante sobre a anulação automática da solicitação por decurso de prazo.

#### Cenário 12: Expiração automática de reivindicação por timeout no Fluxo Social/Digital (48 horas)
- **GIVEN** que o solicitante gerou token temporário para validação via bio de rede social (Nível 3)
- **AND** transcorreram 48 horas sem a validação do código na conta oficial
- **WHEN** o prazo limite de 48 horas expira
- **THEN** o sistema SHALL invalidar o token temporário de validação
- **AND** executar a transição automática da congregação de `Pending_Verification` para `Unclaimed`
- **AND** revogar todas as permissões temporárias
- **AND** liberar o perfil imediatamente para nova reivindicação.

---

### P2: Resolução Automática de Disputa e Contestação Paritária

**História de Usuário**: Como representante legal de uma igreja portando documentação registrada em Cartório (RCPJ) e CNPJ, quero que a apresentação desses documentos revogue sumariamente vínculos anteriores obtidos por métodos sociais ou institucionais sem bloqueio unilateral do titular anterior, assumindo a titularidade legítima do perfil.

**Por que P2**: Protege a soberania jurídica da congregação, impede extorsões ou bloqueios unilaterais por terceiros e garante a fé pública dos registros cartoriais.

#### Cenário 13: Resolução Automática de Disputa por Prevalência Documental Legal de Nível 1
- **GIVEN** que a igreja está no estado `Verified` sob posse de um usuário validado via Nível 2 (e-mail institucional) ou Nível 3 (social/GPS)
- **AND** o representante legal legítimo acessa a página pública da igreja e clica em "Contestar Propriedade"
- **WHEN** o representante legal anexa a Ata de Posse da Diretoria registrada em Cartório (RCPJ) e Cartão CNPJ/QSA válido (Nível 1)
- **AND** aceita os termos com declaração de legitimidade sob o art. 299 do Código Penal
- **THEN** o sistema SHALL validar a conformidade documental de Nível 1
- **AND** revogar sumariamente e de forma imediata o vínculo administrativo do detentor anterior (Nível 2 ou 3)
- **AND** transferir a titularidade da igreja para o representante legal de Nível 1 sob status `Verified`
- **AND** notificar o detentor anterior sobre a revogação sumária por prevalência documental legal
- **AND** NÃO DEVE conceder ao detentor anterior direito a bloqueio unilateral ou retenção do processo.

#### Cenário 14: Disputa paritária entre documentos de Nível 1 com congelamento imediato (In_Dispute)
- **GIVEN** que a igreja está no estado `Verified` sob titular validado em Nível 1
- **WHEN** um segundo solicitante também submete documentação formal de Nível 1 (Ata RCPJ/QSA) contestando a vigência da atual diretoria
- **THEN** o sistema SHALL alterar o status da igreja imediatamente para `In_Dispute`
- **AND** manter os dados públicos da congregação (localização, fotos atuais e horários de cultos) visíveis no mapa para livre consulta comunitária
- **AND** bloquear imediatamente qualquer edição de perfil, alteração de membros e gestão financeira/chaves PIX para ambos os litigantes
- **AND** exibir no perfil público o aviso "Perfil em processo de verificação de titularidade"
- **AND** notificar formalmente ambas as partes estipulando prazo improrrogável de 5 dias úteis para apresentação de Certidão de Breve Relato ou Inteiro Teor atualizada do RCPJ.

#### Cenário 15: Resolução de conflito paritário por Prevalência Registral (averbação mais recente)
- **GIVEN** que a igreja está no estado `In_Dispute` com prazo de 5 dias úteis em andamento
- **AND** a Parte A anexa Certidão de Breve Relato do RCPJ comprovando ata de eleição com averbação datada de 15/08/2026
- **AND** a Parte B anexa Certidão do RCPJ referente a mandato com averbação anterior datada de 10/02/2024
- **WHEN** a moderação técnica conclui a conferência documental tempestiva
- **THEN** o sistema SHALL aplicar o princípio da Prevalência Registral
- **AND** conceder a titularidade definitiva à Parte A com status `Verified` (Nível 1)
- **AND** revogar sumariamente o acesso da Parte B
- **AND** desbloquear os recursos de edição, membros e financeiro para a Parte A
- **AND** remover o aviso público de disputa.

#### Cenário 16: Desclassificação sumária por inércia ao término do prazo de 5 dias úteis
- **GIVEN** que a congregação está em `In_Dispute` e a Parte A enviou Certidão atualizada do RCPJ dentro do prazo
- **AND** a Parte B não anexou qualquer certidão comprobatória dentro da janela de 5 dias úteis
- **WHEN** o prazo fatal de 5 dias úteis expira
- **THEN** o sistema SHALL desclassificar sumariamente a Parte B por inércia processual
- **AND** confirmar a titularidade da congregação em favor da Parte A sob status `Verified` (Nível 1)
- **AND** notificar ambas as partes sobre a conclusão do processo por preclusão da parte inerte.

#### Cenário 17: Litígio irresolvível com anulação do claim e retorno a Unclaimed
- **GIVEN** que a congregação está em `In_Dispute` e ambas as partes apresentam certidões inconclusivas com averbações contraditórias de serventias distintas ou fraude mútua
- **WHEN** a moderação técnica constata a existência de dúvida jurídica insanável administrativamente
- **THEN** o sistema SHALL anular de ofício ambas as reivindicações concorrentes
- **AND** transitar o status da congregação de `In_Dispute` para `Unclaimed`
- **AND** revogar todos os acessos administrativos concedidos a ambos os litigantes
- **AND** emitir notificação formal orientando as partes a buscarem a resolução do litígio na via judicial competente.

---

## Dimensões de Requisitos Implícitos (Sweep)

| Dimensão | Cobertura na Especificação |
| -------- | -------------------------- |
| **Validação de Entrada e Limites** | Raio de geofencing de 100m a partir das coordenadas oficiais; precisão horizontal do GPS ≤ 50m; cálculo exclusivo server-side (Haversine ou espacial); bloqueio de mock locations; erro padronizado `FORA_DO_RAIO_PERMITIDO`; formato de CNPJ/CPF; fotos em tempo real; validade de OTP (15 min); validade de token de bio e timeout social (48h); TTL documental (7 dias); lembrete preventivo de 24h; janela improrrogável de 5 dias úteis para certidões do RCPJ em disputa. |
| **Estados de Falha e Parciais** | Retorno automático a `Unclaimed` por timeout de inatividade (7d documental / 48h social) com descarte de rascunhos, revogação de permissões e liberação imediata do perfil; congelamento em `In_Dispute` mantendo visibilidade no mapa e bloqueando edições/PIX; desclassificação por inércia; retorno a `Unclaimed` em litígio irresolvível. |
| **Idempotência e Concorrência** | Lock de concorrência atômico ao iniciar claim; desempate com prioridade absoluta para submissões documentais Tier 2 / Nível 1. |
| **Fronteiras de Autenticação e Rate Limits** | Requer autenticação prévia; máximo de 3 tentativas por claim; lockout de 72h após 3 falhas consecutivas. |
| **Ciclo de Vida de Dados e Expiração** | Documentos cartorários armazenados com encriptação e segregação; retenção de logs obrigatória de no mínimo 6 meses (180 dias) em repositório seguro append-only, com expiração/arquivamento automatizado pós-prazo para estrita conformidade com a LGPD; expiração automática de claims pendentes por TTL. |
| **Observabilidade e Auditoria** | Coleta obrigatória e padronizada de `client_ip`, `client_port`, `timestamp_utc`, `user_agent` e `verification_metadata` em repositório append-only para todo evento de claim, cadastro e disputa. |
| **Segurança Jurídica e Compliance** | Declaração sob as penas do art. 299 CP (Falsidade Ideológica); enquadramento como Provedora de Aplicação de Internet; cláusula de mera intermediária técnica nos Níveis 2 e 3; isenção funcionalmente vinculada a mecanismo ativo de Takedown (Marco Civil); solução por prevalência registral e saída judicial em litígios complexos. |
| **Integridade de Transição de Estado** | Apenas as 7 transições autorizadas na máquina de estados são aceitas; bloqueio de saltos diretos ilegais. |

---

## Rastreabilidade de Requisitos

| ID do Requisito | História / Área | Fase | Status |
| --------------- | --------------- | ---- | ------ |
| CLAIM-01 | P1: Ciclo de Vida e Estados (Máquina de Estados: Unclaimed, Pending, Verified, In_Dispute e Timeouts) | Specify | Pendente |
| CLAIM-02 | P1: Validação por Presença Física (Geofencing 100m, Accuracy ≤ 50m, Anti-Mock, Haversine Backend e Foto ao vivo) - Nível 3 | Specify | Pendente |
| CLAIM-03 | P1: Validação por Redes Sociais Oficiais (Código na Bio) - Nível 3 | Specify | Pendente |
| CLAIM-04 | P1: Validação por Canais Institucionais Proprietários (Domínio Próprio / E-mail com OTP) - Nível 2 | Specify | Pendente |
| CLAIM-05 | P1: Validação Documental Pública e Cartorial (Ata de Posse RCPJ, Estatuto, CNPJ/QSA) - Nível 1 | Specify | Pendente |
| CLAIM-06 | P1: Termos de Uso, Declaração sob art. 299 CP, Enquadramento como Provedora de Aplicação e Mecanismo Ativo de Takedown | Specify | Pendente |
| CLAIM-07 | P1: Trilha de Auditoria e Logs Obrigatórios conforme Marco Civil da Internet (Lei 12.965/2014) | Specify | Pendente |
| CLAIM-08 | P1: Níveis de Permissão por Hierarquia Probatória (Nível 1 pleno/PIX vs Níveis 2 e 3 operacionais) | Specify | Pendente |
| CLAIM-09 | P2: Tratamento de Concorrência, Precedência por Nível Probatório, Rate Limits e TTLs de Expiração | Specify | Pendente |
| CLAIM-10 | P2: Regra de Resolução Automática de Disputa (Prevalência de Nível 1 sobre Níveis 2 e 3 sem Bloqueio Unilateral) | Specify | Pendente |
| CLAIM-11 | P2: Contestação Paritária de Nível 1 e Congelamento em In_Dispute (Dados Visíveis no Mapa, Edições Bloqueadas e Janela de 5 Dias Úteis) | Specify | Pendente |
| CLAIM-12 | P2: Resolução de Disputa Paritária (Prevalência Registral, Desclassificação por Inércia e Reversão a Unclaimed em Litígio Irresolvível) | Specify | Pendente |

**Cobertura:** 12 requisitos estruturados, 0 mapeados para tarefas técnicas, 12 aguardando confirmação da especificação.

---

## Critérios de Sucesso

- [ ] 100% dos processos de reivindicação simplificada (Níveis 2 e 3) exigem aceite explícito da cláusula declaratória de que a plataforma atua como Provedora de Aplicação e mera intermediária técnica (Lei 12.965/2014) sob as penas do art. 299 do Código Penal.
- [ ] O mecanismo de "Denúncia / Notificação e Retirada" permanece ativo de forma ininterrupta, garantindo o congelamento imediato (`In_Dispute`) ou transferência sumária perante provocação documental de Nível 1 como condição de eficácia da isenção civil.
- [ ] 100% dos eventos de claim e disputa geram logs invioláveis em repositório append-only contendo `client_ip`, `client_port`, `timestamp_utc`, `user_agent` e `verification_metadata`, retidos por no mínimo 6 meses (180 dias) com expiração automatizada pós-prazo conforme o Marco Civil da Internet (art. 15) e a LGPD.
- [ ] Reivindicações pendentes sem submissão de evidências expiram e transitam automaticamente para `Unclaimed` em 7 dias corridos (fluxo documental) ou 48 horas (fluxo social/digital), revogando permissões temporárias e liberando o perfil imediatamente para nova reivindicação, com notificação de lembrete enviada 24 horas antes do timeout.
- [ ] 100% das validações presenciais (Nível 3) exigem cálculo server-side via Haversine/espacial em raio de até 100 metros das coordenadas centrais da igreja, precisão de GPS horizontal (`accuracy`) ≤ 50m, recusa ativa de mock locations e captura fotográfica em tempo real, retornando erro explicativo `FORA_DO_RAIO_PERMITIDO` em distâncias superiores.
- [ ] Usuários validados por métodos de Nível 3 ou Nível 2 não conseguem cadastrar chaves PIX, alterar dados cadastrais críticos ou transferir titularidade (recursos exclusivos de Nível 1).
- [ ] Contestações comprovadas com documentação válida de Nível 1 revogam sumariamente e de imediato vínculos obtidos via Nível 2 ou 3, notificando o titular anterior sem direito a bloqueio unilateral.
- [ ] Contestações paritárias entre documentos de Nível 1 congelam o perfil em `In_Dispute` mantendo dados públicos visíveis no mapa e bloqueando edições/PIX, com janela de 5 dias úteis para certidões do RCPJ, resolvendo por prevalência registral, desclassificação sumária por inércia ou reversão a `Unclaimed` com orientação judicial em caso de dúvida insanável.
