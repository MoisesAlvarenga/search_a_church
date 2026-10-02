# Especificação: Encontrar igreja pelo perfil

## Objetivo
Permitir que o usuário descubra igrejas compatíveis com seu perfil pessoal e localização.

## Contexto
Usuários em viagem ou mudança de cidade precisam encontrar uma igreja que combine com seu estilo de culto, idioma, público e distância.

## Critérios de aceitação
- [ ] Usuário pode selecionar um perfil de busca (ex.: estilo litúrgico, tamanho, idioma)
- [ ] Usuário pode criar ou carregar seu perfil pessoal para usar no match
- [ ] Igrejas cadastradas podem definir perfis que influenciam o match
- [ ] Igrejas não cadastradas são listadas com informações do mapa
- [ ] Os resultados mostram nome, endereço, horário, distância, origem (app ou mapa) e pontuação de match
- [ ] Os resultados são exibidos em uma lista ordenada do maior para o menor match
- [ ] As igrejas retornadas são exibidas em um mapa

## Cenários
### Cenário 1: Buscar igreja por perfil e ver no mapa
- Dado que o usuário define um perfil de igreja (por exemplo, "evangélica contemporânea")
- E o usuário define uma localização de busca
- Quando ele solicita a busca
- Então o sistema retorna igrejas que correspondem ao perfil
- E exibe essas igrejas como marcadores no mapa

### Cenário 2: Ajustar perfil e refinar resultados
- Dado que o usuário já fez uma busca inicial
- Quando ele ajusta o perfil para outro estilo ou idioma
- Então o sistema atualiza os resultados para refletir o novo perfil
- E mantém a visualização no mapa com as novas igrejas
