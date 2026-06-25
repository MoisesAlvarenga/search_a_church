# Banco de Dados

## Modelo de Dados
- Entidades principais: Igreja, Evento, Membro e Avaliação.
- Relacionamentos claros entre igrejas e eventos, membros e participações.

## Persistência
- Usar Entity Framework Core ou outra abordagem leve para a camada de dados.
- Considerar migrações para evoluções de schema.

## Requisitos
- Suporte a consultas por localização e filtros de data.
- Índices nas colunas de busca mais usadas.
