# Especificação: Cadastro de perfis e matching

## Objetivo
Definir como usuários e igrejas se cadastram e como o sistema usa esses perfis para fazer o match, incluindo igrejas listadas diretamente a partir de dados do mapa.

## Contexto
O match entre usuário e igreja deve considerar perfis definidos por ambos. Além disso, igrejas não cadastradas no app podem ser exibidas usando informações extraídas do mapa, garantindo que o usuário tenha acesso a opções relevantes mesmo sem cadastro prévio.

## Critérios de aceitação
- [ ] Usuários podem criar e editar seus perfis de busca
- [ ] Igrejas podem registrar seus perfis no sistema
- [ ] O sistema usa atributos de perfis para combinar usuário x igreja
- [ ] Igrejas não cadastradas no app são exibidas com informações do mapa
- [ ] O sistema indica a origem da igreja nos resultados (`app` ou `maps`)
- [ ] O sistema expõe avaliação média e comentários quando disponíveis
- [ ] Os resultados são ordenados por pontuação de match, da igreja mais compatível para a menos compatível
- [ ] O match prioriza igrejas cadastradas e mapeadas, mas não exclui resultados externos quando há informações úteis

## Cenários
### Cenário 1: Usuário cadastra perfil e realiza busca
- Dado que o usuário criou um perfil com preferência por "culto contemporâneo" e "inglês"
- Quando ele realiza a busca a partir de sua localização atual
- Então o sistema retorna igrejas compatíveis com esse perfil
- E exibe tanto igrejas cadastradas quanto igrejas do mapa
- E cada resultado indica se foi encontrado no app ou no mapa

### Cenário 2: Igreja cadastrada com perfil disponível para match
- Dado que uma igreja cadastrada definiu seu perfil como "tradicional" e "português"
- Quando um usuário busca por perfil compatível
- Então a igreja cadastrada aparece nos resultados com confiança de match
- E sua origem é marcada como `app`

### Cenário 3: Igreja não cadastrada aparece via dados do mapa
- Dado que uma igreja existe no mapa, mas não está cadastrada no app
- Quando o usuário busca por localização e perfil
- Então a igreja ainda pode ser listada com informações do mapa
- E sua origem é marcada como `maps`
