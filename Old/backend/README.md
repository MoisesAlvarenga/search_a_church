# Backend .NET Minimal API (scaffold)

Este diretório conterá o projeto backend .NET Minimal API.

## Inicialização rápida (Windows / PowerShell)
```powershell
dotnet new web -o src -n SearchAChurch.Api
cd src
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Swashbuckle.AspNetCore
```

## Observações
- O projeto ainda precisa de models, controllers/endpoints e configuração de banco.
- Use `docs/specs/` como fonte de verdade para contratuais de API.
