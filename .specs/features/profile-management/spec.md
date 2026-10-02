# Especificação de Gestão de Perfis

## Problema

A relevância da busca depende das preferências do usuário e das características da igreja. O produto precisa de perfis simples e reutilizáveis para evitar a repetição de preferências e permitir comparação justa de igrejas cadastradas.

## Objetivos

- [ ] Usuários podem criar e recuperar um perfil com suas preferências de busca.
- [ ] Igrejas cadastradas podem informar os dados mínimos necessários para Descoberta de Igrejas.
- [ ] Perfis de igrejas podem ser criados a partir da vinculação com uma localização externa do Google Maps.
- [ ] Dados incompletos são representados de forma transparente sem impedir a busca de igrejas provenientes do mapa.

## Fora do Escopo

| Funcionalidade | Motivo |
| -------------- | ------ |
| Gestão administrativa de igrejas | Explicitamente fora do MVP legado. |
| Ciclo de vida da identidade de autenticação | Definido em `authentication-authorization`. |
| Ranqueamento de resultados | Definido em `search-discovery`. |
| Importação e renderização de dados externos de mapas | Definida em `maps-integration`. |

---

## Hipóteses e Questões em Aberto

| Hipótese / decisão | Padrão adotado | Justificativa | Confirmada? |
| ------------------ | -------------- | ------------- | ----------- |
| Edição do perfil da igreja | Apenas representante autorizado pode criar ou alterar o perfil. | A documentação legada prevê perfis de igreja, mas não define propriedade. | Não |
| Campos do perfil de usuário | Idiomas, estilos de culto, tipos de culto e distância máxima desejada. | Campos explícitos na especificação legada. | Não |
| Campos do perfil de igreja | Atributos de perfil, tipo de culto, horário, nome, endereço, posição geográfica e identificador externo (`place_id`). | Necessários para o resultado de descoberta documentado e vinculação ao mapa. | Não |
| Vinculação com Google Maps | O identificador externo do provedor (`place_id`) e as coordenadas geográficas podem ser associados ao perfil da igreja no momento do cadastro. | Permite vincular a igreja localizada no mapa ao perfil oficial sem duplicidade. | Não |
| Exclusão de perfil | Não há comportamento de exclusão no MVP inicial. | O legado só define criação e consulta. | Não |
| Perfil salvo versus filtros diretos | Preferências diretas podem complementar o perfil salvo; a precedência em conflitos requer confirmação. | Ambas as formas aparecem no contrato legado. | Não |

**Questões em aberto:** nenhuma. Todo comportamento não resolvido está registrado acima.

---

## Histórias de Usuário

### P1: Salvar Preferências de Busca MVP

**História de Usuário**: Como usuário, quero salvar minhas preferências de busca de igreja para que buscas futuras reflitam minhas necessidades.

**Por que P1**: Preferências salvas são uma fonte principal de relevância no MVP.

**Critérios de Aceitação**:

1. QUANDO um usuário autenticado enviar um perfil válido ENTÃO o sistema DEVE salvar idiomas preferidos, estilos de culto, tipos de culto e distância máxima desejada.
2. QUANDO um usuário solicitar seu perfil salvo ENTÃO o sistema DEVE retornar o perfil ou um resultado explícito de não encontrado.
3. QUANDO um campo do perfil for inválido ou exceder seus limites aceitos ENTÃO o sistema DEVE rejeitar o perfil e identificar o campo inválido.

**Teste Independente**: Criar um perfil válido, recuperá-lo e tentar criar outro com distância inválida ou valor de campo não suportado.

---

### P1: Cadastrar Informações de Busca da Igreja MVP

**História de Usuário**: Como representante autorizado de uma igreja, quero cadastrar informações básicas e atributos de perfil para que pessoas possam descobrir e comparar a igreja.

**Por que P1**: Dados de igrejas cadastradas são necessários para resultados confiáveis pertencentes ao produto.

**Critérios de Aceitação**:

1. QUANDO um representante autorizado enviar um perfil de igreja válido ENTÃO o sistema DEVE salvar identificação, localização, atributos de perfil, tipo de culto e horário.
2. QUANDO uma igreja cadastrada não tiver um campo opcional de perfil ENTÃO o sistema DEVE preservar o registro e expor o campo como indisponível.
3. QUANDO a localização da igreja estiver ausente ou inválida ENTÃO o sistema DEVE rejeitar o cadastro, pois a igreja não pode participar da descoberta por localização.
4. QUANDO o cadastro de perfil for iniciado a partir de uma igreja do mapa ENTÃO o sistema DEVE vincular o identificador do provedor (`place_id`) e os dados geográficos recebidos ao novo perfil da igreja, marcando a localização como associada.

**Teste Independente**: Cadastrar uma igreja com campos obrigatórios, recuperar seus dados pesquisáveis e verificar que dados geográficos inválidos são rejeitados, bem como testar o vínculo do `place_id` recebido do mapa.

## Casos de Borda

- QUANDO um usuário repetir a criação após resultado de rede incerto ENTÃO o sistema DEVE impedir perfis duplicados não intencionais; o mecanismo de idempotência requer confirmação.
- QUANDO dois representantes tentarem alterar o mesmo perfil de igreja ENTÃO o sistema DEVE preservar a integridade dos dados; o comportamento de conflito requer confirmação.
- QUANDO o cadastro tentar vincular um identificador de mapa (`place_id`) que já pertence a outro perfil ativo ENTÃO o sistema DEVE rejeitar a vinculação duplicada e sinalizar conflito de propriedade.
- QUANDO uma igreja não estiver cadastrada no produto ENTÃO o sistema NÃO DEVE impedir sua aparição pelo recurso independente de descoberta por mapa.

## Rastreabilidade de Requisitos

| ID do Requisito | História | Fase | Status |
| --------------- | -------- | ---- | ------ |
| PROFILE-01 | P1: Salvar Preferências de Busca | Specify | Pendente |
| PROFILE-02 | P1: Salvar Preferências de Busca | Specify | Pendente |
| PROFILE-03 | P1: Cadastrar Informações de Busca da Igreja | Specify | Pendente |
| PROFILE-04 | P1: Cadastrar Informações de Busca da Igreja | Specify | Pendente |
| PROFILE-05 | P1: Cadastrar Informações de Busca da Igreja | Specify | Pendente |

**Cobertura:** 5 no total, 0 mapeados para tarefas, 5 não mapeados aguardando confirmação da especificação.

## Critérios de Sucesso

- [ ] Um perfil de usuário salvo pode ser usado como entrada para uma busca de descoberta.
- [ ] Uma igreja cadastrada possui as informações necessárias para comparação por localização.
- [ ] A vinculação do identificador externo (`place_id`) ao perfil da igreja é preservada para integração com o mapa.
- [ ] Dados de perfil inválidos não conseguem entrar silenciosamente no fluxo de descoberta.