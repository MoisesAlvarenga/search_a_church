# MVP do Search a Church

## Valor principal do produto
Criar uma plataforma de descoberta de igrejas para quem está viajando ou procurando uma nova igreja, permitindo encontrar locais compatíveis com o perfil do usuário e a distância desejada, e visualizar esses resultados em um mapa.

## Usuário alvo
- Viajantes que querem encontrar uma igreja próxima ao local onde estão.
- Pessoas em mudança de cidade que desejam descobrir igrejas compatíveis com seu estilo de culto.
- Usuários que buscam igrejas por perfil, localização e distância no contexto de viagem ou pesquisa local.

## Proposta de valor
- Encontre igrejas relevantes rapidamente.
- Veja a localização das igrejas no mapa.
- Filtre resultados por proximidade, perfil e preferências pessoais.
- Permita que usuários e igrejas registrem seus perfis para melhorar o match.
- Listar também igrejas não cadastradas diretamente no app a partir de dados do mapa.

## Características essenciais do MVP
1. Busca por localização e distância
   - Permitir busca a partir de endereço, cidade ou localização atual.
   - Utilizar distância como um dos fatores de ordenação, mas não como único critério de exclusão.
   - Exibir resultados ordenados por relevância de match.

2. Perfis de usuário / tipos de igreja
   - Usuários podem criar perfis que representam estilo de culto, tamanho, idioma e preferências.
   - Igrejas podem cadastrar perfis básicos para facilitar o match.
   - Igrejas não cadastradas no app ainda podem ser listadas com informações vindas do mapa.
   - Agrupar igrejas por atributos relevantes (ex.: liturgia, tamanho, idioma, serviços familiares).

3. Visualização em mapa
   - Exibir resultados georreferenciados em um mapa interativo.
   - Mostrar ícones ou marcadores que representam a posição de cada igreja.

4. Informações essenciais da igreja
   - Exibir nome, endereço, horário de culto e atributos do perfil.
   - Incluir distância estimada em relação à localização pesquisada.

5. Sistema de feedback
   - Permitir avaliações por estrelas e comentários das igrejas.
   - Exibir média de avaliação e comentários mais relevantes nos resultados.
   - Oferecer feedback tanto para igrejas cadastradas quanto para resultados do mapa.

6. Repositórios e organização
   - Repositório geral: regras, especificações, decisões e documentação.
   - Repositório backend: implementação da API e lógica de busca.
   - Repositório frontend: app Angular, mapa e interface de descoberta.

## O que fica fora do MVP
- Gestão administrativa de igrejas (cadastro completo de igreja pelo administrador).
- Controle detalhado de membros, eventos ou voluntariado.
- Recursos avançados como recomendações personalizadas baseadas em histórico.

## Objetivo do MVP
Validar rapidamente a ideia de que viajantes e buscadores de igreja conseguem encontrar locais relevantes e visualizá-los no mapa usando filtros simples de perfil e distância.

## Métricas de sucesso iniciais
- Usuário consegue filtrar e visualizar igrejas em até 3 passos.
- Resultados aparecem no mapa e mostram distância.
- O fluxo de busca corresponde ao perfil escolhido pelo usuário.
