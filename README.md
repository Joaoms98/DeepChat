# DeepChat

Internal LAN chat over .NET 8 + ASP.NET Core SignalR. End-to-end encrypted with AES-256-GCM, ephemeral history (≤ 24 h), IP whitelist, JWT auth, self-contained single-file deployment.

## Status

| Layer | What's done |
|---|---|
| Domain | Entities, value objects, use cases (Auth) |
| Crypto | AES-256-GCM with AAD, Argon2id (3 profiles), key store |
| Infrastructure | EF Core 8 + SQLite, repositories, JWT issuer, Argon2id password hasher |
| Server | Auth endpoint, IP whitelist middleware, JWT bearer, SignalR ChatHub, BackgroundService de purge (24 h), rate limiter |
| Client | Spectre.Console CMD client with login/register, room key derivation, SignalR connect, command router |

**274 unit + integration tests passing**, 4 hub integration tests skipped (`WebApplicationFactory` + JWT bearer interaction issue — covered end-to-end by running real client+server).

## Architecture

```
DeepChat/
├── src/
│   ├── Galileo.Chat.Domain/          Entities, VOs, abstractions, use cases
│   ├── Galileo.Chat.Crypto/          AES-GCM, Argon2id, key store, secure RNG
│   ├── Galileo.Chat.Shared/          DTOs / Hub contracts (cliente↔servidor)
│   ├── Galileo.Chat.Infrastructure/  EF Core, SQLite, JWT, Argon2 hasher
│   ├── Galileo.Chat.Server/          ASP.NET Core host + SignalR hub
│   ├── Galileo.Chat.Client.Core/     Lógica de cliente reutilizável (SignalR, crypto, auth) — console + GUI
│   ├── Galileo.Chat.Client/          Console + Spectre.Console
│   └── Galileo.Chat.Client.Gui/      Cliente gráfico WPF (net9.0-windows)
└── tests/
    ├── Galileo.Chat.Domain.Tests/         (102 tests)
    ├── Galileo.Chat.Crypto.Tests/         (68 tests)
    ├── Galileo.Chat.Infrastructure.Tests/ (44 tests)
    ├── Galileo.Chat.Server.Tests/         (36+4 tests)
    └── Galileo.Chat.Client.Tests/         (9 tests)
```

## Build

A maior parte da solução é `net8.0`. O cliente gráfico (`Galileo.Chat.Client.Gui`)
é WPF e alvo **`net9.0-windows`**, então **buildar a solução completa exige o SDK
.NET 9** (um alvo net9 não compila sob SDK 8). Por isso o `global.json` usa
`rollForward: latestMajor` — com o SDK 9 instalado, ele é selecionado
automaticamente; o restante dos projetos continua compilando como net8.0.

> A GUI é Windows-only (WPF). Em Linux/macOS ela degrada para um placeholder
> net8.0, então `dotnet build` da solução não quebra fora do Windows — mas a
> interface gráfica de fato só roda no Windows.

```powershell
dotnet tool restore
dotnet build
dotnet test
```

## Run (development)

Two terminals.

**Server:**

```powershell
cd src\Galileo.Chat.Server
dotnet run
# Listens on http://localhost:5000
```

**Client (console):**

```powershell
cd src\Galileo.Chat.Client
dotnet run
# Prompts for server URL, credentials (offers register), room name, room passphrase.
```

**Client (GUI / WPF):**

```powershell
cd src\Galileo.Chat.Client.Gui
dotnet run
# Janela: login/registro → escolher/criar sala + passphrase → tela de chat.
```

Ambos os clientes compartilham `Galileo.Chat.Client.Core` (conexão SignalR, crypto, auth)
e leem `appsettings.json` (seção `Client`) para o `ServerUrl`. A GUI alvo `net9.0-windows`
porque só o WindowsDesktop runtime 9.x está instalado nas máquinas de dev; o restante da
solução continua em `net8.0`.

## Hospedando o servidor numa LAN

Três scripts em `scripts/` numerados pela ordem que devem rodar:

| Script | Quando rodar | Privilégio |
|---|---|---|
| `0-setup-firewall.ps1` | **Uma vez** por máquina (libera TCP 5666) | Admin |
| `1-publish.ps1` | Após `git clone` ou quando o código mudar (gera os EXEs) | Normal |
| `2-host-server.ps1` | **Toda vez** que for hostar (detecta IP, atualiza ZIP, sobe server) | Admin (recomendado) |

### Passo 0 — Firewall (uma vez por PC, ADMIN)

Em PowerShell elevado (Win+X → Terminal Admin):

```powershell
.\scripts\0-setup-firewall.ps1
```

Cria a regra `DeepChat` autorizando inbound TCP 5666. Idempotente — pode rodar várias vezes sem acumular regras.

### Passo 1 — Publish (uma vez por checkout)

```powershell
.\scripts\1-publish.ps1
# → publish\server\Galileo.Chat.Server.exe
# → publish\client\deepchat.exe
```

Demora 1–2 min. Ambos os EXEs rodam em Windows limpo, sem .NET instalado.

### Passo 2 — Subir o servidor (toda vez, ADMIN recomendado)

```powershell
.\scripts\2-host-server.ps1
```

O script faz tudo numa só execução:

1. Detecta o IP local da rede atual (ex: `192.168.1.42`)
2. Atualiza `publish\share\appsettings.json` com `http://<seu-ip>:5666`
3. Regera `publish\deepchat-cliente.zip` pra distribuir
4. Marca a rede ativa como **Private** (se invocado como admin)
5. Confere a regra de firewall TCP 5666
6. Sobe o server em `0.0.0.0:5666` (Ctrl+C pra parar)

> Sem admin: o script ainda detecta IP, atualiza JSON e ZIP, sobe o server. Apenas o passo 4 precisa de admin — ele imprime o comando exato pra rodar manualmente se for o caso.

### Por que o perfil tem que ser Private

Por default, toda Wi-Fi nova começa como **Pública** no Windows. Nesse perfil, o Defender Firewall é mais agressivo e **descarta pacotes inbound silenciosamente**, mesmo com `New-NetFirewallRule` autorizando a porta. Resultado: o cliente do outro PC dá `tentativa de conexão falhou porque o componente conectado não respondeu` (timeout silencioso). Marcar como Privada destrava.

### Como saber se está tudo certo

```powershell
# 1. Server está vivo e respondendo
Invoke-WebRequest http://localhost:5666/health -UseBasicParsing

# 2. Outro PC consegue alcançar você (rodar NO outro PC)
Test-NetConnection -ComputerName <seu-ip> -Port 5666
# TcpTestSucceeded: True → tudo OK
```

### Distribuir o ZIP

`publish\deepchat-cliente.zip` (~30 MB) contém `deepchat.exe` + `appsettings.json` (já com seu IP) + `LEIAME.txt`. Manda pelo WhatsApp/Drive/pendrive. O receptor extrai numa pasta qualquer, double-click no `deepchat.exe`. Sem instalação, sem `.NET`.

## Deploy checklist

- [ ] Generate strong `DEEPCHAT_JWT_SECRET` (env var or `appsettings.Production.json`).
- [ ] Configure `Network:AllowedIps` with your LAN range (CIDR).
- [ ] Generate self-signed TLS cert and bind Kestrel to a LAN IP (not `0.0.0.0`).
- [ ] Distribute room passphrases offline (verbal, paper, password manager).
- [ ] Verify `Retention:MessageTtlHours = 24` (default).
- [ ] Confirm logs do not contain plaintext / ciphertext / JWTs / passwords.

## Security model (summary)

- **Server cannot read messages.** Payloads are encrypted client-side with a key derived from a shared room passphrase via Argon2id; the server only stores opaque BLOBs.
- **TLS** protects against passive sniffing on the LAN. **AES-GCM E2E** protects against admin/MITM with the cert.
- **AAD** (Associated Data) binds each ciphertext to its `roomId`, so a captured envelope cannot be replayed into another room.
- **Argon2id** (memory=128 MiB, iterations=4) makes brute-forcing room passphrases infeasible.
- **IP whitelist** (with CIDR) runs before authentication.
- **JWT** with 8 h lifetime, revocable via `Sessions` table.
- **Login rate limit**: 5 attempts/minute per IP.
- **Default rate limit**: 30 requests/10 s sustained per IP.
- **Ephemeral history**: messages older than 24 h are deleted by `MessagePurgeService` every 5 min; `VACUUM` runs nightly.

## Commands (in-chat)

| Comando | O que faz |
|---|---|
| `/online` | Lista quem está online na sala atual (com GUID) |
| `/rooms` | Lista todas as salas no servidor |
| `/join <nome>` | Troca de sala em runtime (pede passphrase nova, deriva chave, faz LeaveRoom + JoinRoom no hub atomicamente) |
| `/msg <nick\|guid> <texto>` | Envia mensagem privada (DM). Aceita nickname ou GUID. Se houver nicks duplicados, mostra os candidatos |
| `/clear` | Limpa a tela |
| `/beep` | Liga/desliga o bipe nas mensagens recebidas |
| `/help` | Mostra ajuda |
| `/quit` | Sai do DeepChat |

## What's NOT implemented yet

- **DM com chave par-a-par**: `/msg` hoje usa a chave da sala atual + roteamento exclusivo pro destinatário (servidor garante; outros membros não recebem o blob). DM com ECDH par-a-par é evolução pós-MVP — `IChatHubServer.PostPrivateMessage` já está documentado com esse caveat.
- Cert pinning real no cliente (hoje `AllowInsecureTls=true` aceita self-signed; pinning SHA-256 está planejado).
- DPAPI key store no cliente (hoje `InMemory` — passphrase reentrada a cada launch e a cada `/join`).
- Forward secrecy real (hoje chaves de sala estáticas; Double Ratchet é futuro).
- **Prompt de senha mascarado**: senha e passphrase aparecem em texto enquanto digita. O `Spectre.Console.Secret('•')` original travava em alguns conhost do Windows (digitação não chegava no stdin), então foi removido. Trade-off OK pro modelo "LAN trustada entre amigos".
- HTTPS por default: hoje o servidor sobe em HTTP (configurado via `--urls http://0.0.0.0:5666`). Pra LAN privada OK; pra rede aberta tem que gerar cert auto-assinado e adicionar cert pinning no cliente.
