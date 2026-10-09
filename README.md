# lkfinances.api

API .NET 10 do controle financeiro (RESUMO + GASTOS). O front fica no repositório irmão `lkfinances.web`.

## Rodar no Debian

Clone os dois repositórios lado a lado:

```bash
git clone <api> lkfinances.api
git clone <web> lkfinances.web
cd lkfinances.api
cp .env.example .env    # defina POSTGRES_PASSWORD; BOOTSTRAP_* é opcional
docker compose up -d --build
```

A aplicação fica em `http://<servidor>:8080` (porta em `WEB_PORT`). O Postgres não publica porta no host.

- Crie a conta na tela **Criar conta**. Se `BOOTSTRAP_EMAIL` e `BOOTSTRAP_PASSWORD` estiverem no `.env` **e o banco estiver vazio**, esse usuário é criado na primeira subida. Reiniciar não altera senha.
- Troca de senha: menu **Trocar senha** depois de entrar. Mínimo de 8 caracteres.
- A migration roda quando a API sobe, antes de aceitar requisições.
- Atualizar: `git pull` nos dois repos e `docker compose up -d --build` neste.
- Saúde: `curl http://<servidor>:8080/health`.

### Importar a planilha

```bash
docker compose cp ~/Finanças.xlsx api:/tmp/financas.xlsx
docker compose exec api dotnet LkFinances.Api.dll import --file /tmp/financas.xlsx
```

### Backup

```bash
docker compose exec -T db pg_dump -U lkfinances -Fc lkfinances > lkfinances-$(date +%F).dump
```

## Desenvolvimento

```bash
docker run -d --name lkf-dev-db -e POSTGRES_DB=lkfinances -e POSTGRES_USER=lkfinances \
  -e POSTGRES_PASSWORD=lkfinances -p 127.0.0.1:5433:5432 postgres:16-alpine

dotnet run --project LkFinances.Api --urls http://localhost:5080
```

Cadastre-se na UI, ou use `dev@lkfinances.local` / `dev-password` se o banco estiver vazio.

- Testes: `dotnet test` (precisa do Docker; sobe um Postgres próprio).
- Nova migration: `dotnet ef migrations add <Nome> --project LkFinances.Core --startup-project LkFinances.Api --output-dir Data/Migrations`.
