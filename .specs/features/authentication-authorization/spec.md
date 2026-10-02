# Especificação de Autenticação e Autorização

## Problema

Perfis e feedback contêm dados pertencentes aos usuários, mas o produto legado não tem uma fronteira de identidade concluída. O produto deve proteger escritas e identificar seus autores, mantendo a descoberta pública com baixo atrito.

## Objetivos

- [ ] Usuários públicos podem buscar igrejas sem iniciar sessão.
- [ ] Ações protegidas exigem identidade autenticada.
- [ ] A autorização impede que usuários alterem dados que não possuem nem representam.

## Fora do Escopo

| Funcionalidade | Motivo |
| -------------- | ------ |
| Escolha de provedor de identidade, interface e detalhes de implementação de token | São decisões de Design. |
| Assinatura, pagamento ou faturamento de organizações | Não pertencem ao escopo legado. |
| Recuperação avançada de conta e autenticação multifator | Não há requisito documentado para o MVP. |
| Regras de conteúdo de perfis e avaliações | Definidas nas especificações correspondentes. |

---

## Hipóteses e Questões em Aberto

| Hipótese / decisão | Padrão adotado | Justificativa | Confirmada? |
| ------------------ | -------------- | ------------- | ----------- |
| Abordagem de autenticação | A abordagem prevista para a API é JWT sem estado. | A decisão arquitetural legada seleciona JWT. | Não |
| Descoberta pública | Ler resultados de busca e de mapa não exige autenticação. | A segurança legada descreve acesso público à busca `GET`. | Não |
| Escritas protegidas | Criar ou alterar perfis de usuário, perfis de igreja e avaliações exige autenticação. | A segurança legada protege essas escritas. | Não |
| Autorização de perfil de igreja | Usuário deve ter papel de representante verificado para a igreja que alterar. | O legado não possui modelo de propriedade; esta é a política mínima segura. | Não |
| Expiração, renovação, cadastro e recuperação de token | Não especificados. | A decisão de JWT não define essas regras de ciclo de vida. | Não |
| Abuso e limites de taxa | Não especificados. | É uma lacuna de produto e operação. | Não |

**Questões em aberto:** nenhuma. Todo comportamento não resolvido está registrado como hipótese acima.

---

## Histórias de Usuário

### P1: Acessar Descoberta Pública MVP

**História de Usuário**: Como visitante, quero buscar igrejas sem criar conta para avaliar o produto antes de contribuir com dados.

**Por que P1**: A busca é a experiência principal do MVP e deve ser acessível a viajantes.

**Critérios de Aceitação**:

1. QUANDO visitante não autenticado solicitar uma busca de descoberta ENTÃO o sistema DEVE fornecer a capacidade pública de busca.
2. QUANDO visitante não autenticado solicitar dados públicos de descoberta apoiados por mapa ENTÃO o sistema DEVE aplicar a mesma política de acesso público.
3. QUANDO uma solicitação pública contiver entrada inválida ENTÃO o sistema DEVE retornar resultado de validação sem expor dados protegidos.

**Teste Independente**: Solicitar busca válida e inválida sem autenticação e verificar resultados acessíveis e validação segura.

---

### P1: Proteger Contribuições do Usuário MVP

**História de Usuário**: Como colaborador, quero que meu perfil e feedback sejam associados à minha identidade autenticada para que outras pessoas não possam me representar indevidamente nem alterar meus dados.

**Por que P1**: Perfis e feedback não podem ser confiáveis sem uma fronteira de autoria.

**Critérios de Aceitação**:

1. QUANDO usuário autenticado criar ou alterar seu perfil ENTÃO o sistema DEVE associar o perfil à identidade desse usuário.
2. QUANDO usuário autenticado enviar avaliação ENTÃO o sistema DEVE derivar o autor da identidade do usuário, sem aceitar identidade de autor arbitrária na solicitação.
3. QUANDO pessoa não autenticada tentar escrita protegida ENTÃO o sistema DEVE negar a ação e NÃO DEVE persistir alterações.
4. QUANDO usuário autenticado tentar alterar perfil ou avaliação de outra pessoa sem autorização ENTÃO o sistema DEVE negar a ação e NÃO DEVE persistir alterações.

**Teste Independente**: Executar escritas protegidas como proprietário, anonimamente e como outro usuário autenticado, verificando o estado persistido em cada caso.

---

### P2: Proteger a Representação da Igreja

**História de Usuário**: Como representante de igreja, quero gestão autorizada das informações pesquisáveis da minha igreja para que elas permaneçam confiáveis.

**Por que P2**: O MVP precisa de dados de igreja confiáveis, mas o cadastro de representantes ainda não está definido.

**Critérios de Aceitação**:

1. QUANDO representante verificado criar ou alterar o perfil da igreja que representa ENTÃO o sistema DEVE permitir a ação.
2. QUANDO usuário autenticado sem direito de representação tentar criar ou alterar o perfil da igreja ENTÃO o sistema DEVE negar a ação e NÃO DEVE persistir alterações.
3. QUANDO direitos de representação mudarem ENTÃO o sistema DEVE aplicar a política confirmada de transição de propriedade; essa política requer confirmação.

**Teste Independente**: Tentar a mesma alteração de perfil de igreja com uma identidade representante e outra não representante.

## Casos de Borda

- QUANDO token autenticado estiver expirado ou inválido ENTÃO o sistema DEVE negar ações protegidas sem persistir alterações.
- QUANDO uma escrita protegida for repetida após falha de rede ENTÃO o sistema DEVE preservar a integridade de propriedade e evitar duplicidades não intencionais.
- QUANDO ocorrer falha de autorização ENTÃO o sistema NÃO DEVE revelar informações desnecessárias sobre dados ou privilégios de outra pessoa.

## Rastreabilidade de Requisitos

| ID do Requisito | História | Fase | Status |
| --------------- | -------- | ---- | ------ |
| AUTH-01 | P1: Acessar Descoberta Pública | Specify | Pendente |
| AUTH-02 | P1: Proteger Contribuições do Usuário | Specify | Pendente |
| AUTH-03 | P1: Proteger Contribuições do Usuário | Specify | Pendente |
| AUTH-04 | P2: Proteger a Representação da Igreja | Specify | Pendente |

**Cobertura:** 4 no total, 0 mapeados para tarefas, 4 não mapeados aguardando confirmação da especificação.

## Critérios de Sucesso

- [ ] Visitante consegue usar descoberta pública sem conta.
- [ ] Escritas protegidas são atribuíveis a uma identidade autenticada.
- [ ] Chamadores não autorizados não conseguem criar ou alterar dados de perfil, igreja ou avaliação.