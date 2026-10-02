# Padrões de Código

## .NET
- Usar C# 12 e .NET 8+ se disponível.
- Favor convenções de nomenclatura PascalCase para tipos e camelCase para variáveis locais.
- Evitar classes de serviço com lógica pesada; use funções e pequenos componentes.

## Angular
- Manter componentes pequenos e testáveis.
- Usar `@Injectable` para serviços com escopo adequado.
- Organizar módulos por domínio.

## Testes
- Escrever especificações antes da implementação.
- Focar em comportamentos observáveis e fluxos principais.
- Manter testes determinísticos e rápidos.
