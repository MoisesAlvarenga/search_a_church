# 0003 - Usar JWT

## Decisão
Usar JWT para autenticação e autorização na API.

## Motivação
- Permitir autenticação stateless e escalável.
- Compatível com frontend Angular e mobilidade futura.
- Fácil implementação em .NET Minimal API.

## Consequências
- Necessidade de gerenciamento seguro de chaves/segredos.
- Implementar validação de tokens nos middlewares.
