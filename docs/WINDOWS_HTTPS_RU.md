# HTTPS на порту 443 в Windows без дополнительных программ

Инструкция для разработки в Windows с внешним SQL Server и запуском через
`run-dev.ps1`. HTTPS принимает сам Vite; API остаётся на `http://localhost:5080`.
Запросы `/api` передаются через существующий Vite proxy. Docker не требуется.
Это настройка сервера разработки, а не инструкция по промышленному развёртыванию.

Все команды, кроме явно отмеченных, выполняются из корня репозитория в обычном
PowerShell. Настройте подключение к SQL Server по [README](../README.md).
Если frontend ещё не установлен, выполните:

```powershell
npm.cmd --prefix frontend ci --include=dev
```

## 1. Создать сертификат

Узнайте IPv4 рабочего Ethernet/Wi-Fi адаптера через `ipconfig` и замените пример
`192.168.1.100` ниже своим адресом. При изменении IP сертификат нужно перевыпустить.

```powershell
$ip = "192.168.1.100"
$certDir = Join-Path $env:LOCALAPPDATA "SwiftReview\certs"
New-Item -ItemType Directory -Force -Path $certDir | Out-Null

$cert = New-SelfSignedCertificate `
  -Type SSLServerAuthentication `
  -Subject "CN=$ip" `
  -TextExtension @("2.5.29.17={text}IPAddress=$ip&DNS=localhost&IPAddress=127.0.0.1") `
  -CertStoreLocation "Cert:\CurrentUser\My" `
  -KeyExportPolicy Exportable `
  -NotAfter (Get-Date).AddYears(1)

$password = Read-Host "Задайте пароль для PFX" -AsSecureString
Export-PfxCertificate -Cert $cert -FilePath "$certDir\server.pfx" -Password $password | Out-Null
Export-Certificate -Cert $cert -FilePath "$certDir\server.cer" | Out-Null
```

`server.pfx` содержит закрытый ключ и остаётся на сервере вне репозитория.
`server.cer` содержит публичный сертификат для клиентских компьютеров.
Не записывайте пароль в исходники или документацию.

## 2. Настроить Vite

В `frontend/vite.config.ts` добавьте импорт:

```typescript
import { readFileSync } from 'node:fs';
```

Замените существующий блок `server` следующим. Остальные настройки файла сохраните:

```typescript
server: {
  ...(process.env.SWIFTREVIEW_TLS_PFX
    ? {
        host: '0.0.0.0',
        port: 443,
        strictPort: true,
        https: {
          pfx: readFileSync(process.env.SWIFTREVIEW_TLS_PFX),
          passphrase: process.env.SWIFTREVIEW_TLS_PASSWORD,
        },
      }
    : {}),
  proxy: {
    '/api': {
      target: env.VITE_API_PROXY_TARGET || 'http://localhost:5080',
    },
  },
},
```

HTTPS включается только при наличии `SWIFTREVIEW_TLS_PFX` в окружении процесса.
Без неё сохраняется обычный HTTP-запуск Vite. Эти переменные задаются в PowerShell;
они не используют префикс `VITE_` и не предназначены для браузерного кода.

В `run-dev.ps1` строка аргументов запуска frontend должна оставаться исходной:

```powershell
-ArgumentList "--prefix", "frontend", "run", "dev" `
```

Если ранее добавляли `--port 5173`, удалите его: аргумент командной строки
переопределяет порт из конфигурации. `strictPort` не позволяет незаметно перейти
на другой порт, если 443 уже занят.

После изменения конфигурации выполните проверки frontend:

```powershell
npm.cmd --prefix frontend test
npm.cmd --prefix frontend run typecheck
npm.cmd --prefix frontend run build
npm.cmd --prefix frontend run lint
```

## 3. Разрешить входящие подключения

В PowerShell **от администратора**, один раз:

```powershell
New-NetFirewallRule -DisplayName "SwiftReview HTTPS" `
  -Direction Inbound -Action Allow -Protocol TCP -LocalPort 443 `
  -Profile Domain,Private -RemoteAddress LocalSubnet
```

Правило действует для доменного и частного профилей, только для локальной подсети.
Если клиенты находятся в других корпоративных подсетях, согласуйте с сетевым
администратором разрешённые адреса и доступ между подсетями. Корпоративная политика
может запрещать применение локальных правил firewall.

## 4. Запустить приложение

При каждом запуске в новом PowerShell из корня репозитория:

```powershell
$certDir = Join-Path $env:LOCALAPPDATA "SwiftReview\certs"
$password = Read-Host "Пароль для PFX" -AsSecureString
$env:SWIFTREVIEW_TLS_PFX = Join-Path $certDir "server.pfx"
$env:SWIFTREVIEW_TLS_PASSWORD = [System.Net.NetworkCredential]::new("", $password).Password

try {
    .\run-dev.ps1
}
finally {
    Remove-Item Env:SWIFTREVIEW_TLS_PFX -ErrorAction SilentlyContinue
    Remove-Item Env:SWIFTREVIEW_TLS_PASSWORD -ErrorAction SilentlyContinue
}
```

Откройте `https://192.168.1.100`, подставив IP из шага 1. Указывать `:443` не нужно.
На сервере также доступен `https://localhost`. Порт API 5080 открывать в сеть не нужно.

## 5. Настроить доверие к сертификату

Самоподписанный сертификат изначально не доверен браузерам. Передайте клиентам
только `server.cer`. Для доверия в текущем профиле Windows, если это разрешено
корпоративными правилами, выполните на каждом клиенте и на сервере:

```powershell
Import-Certificate -FilePath "C:\путь\server.cer" -CertStoreLocation "Cert:\CurrentUser\Root"
```

Затем перезапустите браузер. Браузеры с отдельным хранилищем сертификатов могут
потребовать дополнительной настройки. В корпоративной сети сертификат и его
распространение также может предоставить внутренний центр сертификации.

## Проверка проблем с доступом

На сервере:

```powershell
Get-NetTCPConnection -State Listen -LocalPort 443 |
  Select-Object LocalAddress, LocalPort, OwningProcess

Get-NetConnectionProfile |
  Select-Object InterfaceAlias, NetworkCategory
```

Vite должен слушать `0.0.0.0:443`. Если порт уже занят, определите процесс по
`OwningProcess`; не останавливайте неизвестную корпоративную службу.

На другом Windows-компьютере:

```powershell
Test-NetConnection 192.168.1.100 -Port 443
```

- `TcpTestSucceeded: False`: проверяйте IP, firewall и доступ между подсетями.
  HTTPS сам по себе не снимает сетевые ограничения.
- `TcpTestSucceeded: True`, но браузер сообщает об ошибке сертификата: проверьте
  доверие к `server.cer`, срок действия сертификата и совпадение IP в адресной строке.
- Страница открывается, но данные не загружаются: проверьте API на
  `http://localhost:5080/health` непосредственно на сервере, подключение к SQL Server
  и `VITE_API_PROXY_TARGET` в корневом `.env`.

## Источники

- [Microsoft: New-SelfSignedCertificate](https://learn.microsoft.com/en-us/powershell/module/pki/new-selfsignedcertificate)
- [Microsoft: Export-PfxCertificate](https://learn.microsoft.com/en-us/powershell/module/pki/export-pfxcertificate)
- [Vite: настройки сервера, HTTPS и proxy](https://vite.dev/config/server-options)
