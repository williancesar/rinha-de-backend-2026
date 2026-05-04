# rinha-de-backend-2026 — fraud detection (.NET 10 AOT + IVF-Flat int8)

Submissão para a [Rinha de Backend 2026](https://github.com/zanfranceschi/rinha-de-backend-2026).

## Stack

- .NET 10 com Native AOT (C# 14)
- Busca vetorial: IVF-Flat com K=256 clusters, vetores em int8
- haproxy 3.0-alpine como round-robin LB
- 2 réplicas API + 1 LB; 1 CPU + 350 MB total

## Como rodar localmente

```bash
# build da imagem (gera index.bin durante o build, ~30-60s)
docker build -t rinha-fraud-api:local -f infra/Dockerfile .

# subir compose local (testes manuais)
cd infra && docker compose up

# em outro terminal:
curl http://localhost:9999/ready
curl -X POST http://localhost:9999/fraud-score \
  -H 'Content-Type: application/json' \
  -d @../resources/example-payloads.json  # editar para um único payload
```

## Como rodar os testes do host

Clonar `zanfranceschi/rinha-de-backend-2026`. Subir esta stack. Em outro terminal:

```bash
cd /caminho/para/rinha-de-backend-2026
k6 run test/smoke.js   # smoke (5 requests)
./run.sh               # load test completo (120s)
```

## Branches

- `main` — código fonte
- `submission` — apenas `docker-compose.yml`, `haproxy.cfg`, `info.json` (consumido pelo workflow do host)
