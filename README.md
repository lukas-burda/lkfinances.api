# lkfinances.api

API .NET 10 do controle financeiro pessoal (RESUMO + GASTOS).

O front está em [lkfinances.web](https://github.com/lukas-burda/lkfinances.web). Os dois repositórios ficam lado a lado.

```
lkfinances.api/     este repo
lkfinances.web/     UI React
```

## Desenvolvimento no PC

Postgres na porta `5433`, API em `http://localhost:5080`.

```bash
docker run -d --name lkf-dev-db -e POSTGRES_DB=lkfinances -e POSTGRES_USER=lkfinances -e POSTGRES_PASSWORD=lkfinances -p 127.0.0.1:5433:5432 postgres:16-alpine

cd lkfinances.api
dotnet run --project LkFinances.Api --urls http://localhost:5080
```

No outro terminal:

```bash
cd lkfinances.web
npm install
npm run dev
```

Abra [http://localhost:5173](http://localhost:5173). Crie a conta na tela **Criar conta**.

Se o banco estiver vazio, o Development também cria `dev@lkfinances.local` / `dev-password`.

- Testes: `dotnet test` (sobe um Postgres no Docker).
- Migration: `dotnet ef migrations add <Nome> --project LkFinances.Core --startup-project LkFinances.Api --output-dir Data/Migrations`.

## Debian (Docker)

```bash
git clone https://github.com/lukas-burda/lkfinances.api.git
git clone https://github.com/lukas-burda/lkfinances.web.git
cd lkfinances.api
cp .env.example .env    # defina POSTGRES_PASSWORD
docker compose up -d --build
```

A aplicação fica em `http://<servidor>:8080`. O Postgres não publica porta no host.

- Sem cadastro no `.env`: use **Criar conta** na UI.
- `BOOTSTRAP_EMAIL` / `BOOTSTRAP_PASSWORD` só criam usuário se o banco estiver vazio. Reiniciar não troca senha.
- Atualizar: `git pull` nos dois repos e `docker compose up -d --build` neste.
- Saúde: `curl http://<servidor>:8080/health`.

## Importar CSV ou XLSX

Cabeçalho:

```text
data;descricao;valor;type;status;observacao
```

Valor positivo é entrada; negativo, saída. Aceita `R$ 1.234,56`. `type` vira categoria (`AJUSTE`, `RESERVA`, `INVESTIMENTO`, `MUSEU` ou outro nome). `status` vazio é ativo; `PENDENTE` e `ANULADO` também valem. O mesmo arquivo de novo não duplica linha.

No PC (Development):

```bash
cd lkfinances.api
dotnet run --project LkFinances.Api -- import --file C:\Users\lukas\Downloads\gastos.csv --email seu@email
```

No Debian:

```bash
docker compose cp ~/gastos.csv api:/tmp/gastos.csv
docker compose exec api dotnet LkFinances.Api.dll import --file /tmp/gastos.csv --email seu@email
```

## Backup

```bash
docker compose exec -T db pg_dump -U lkfinances -Fc lkfinances > lkfinances-$(date +%F).dump
```
