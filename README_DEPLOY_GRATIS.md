# MarketForge — Deploy grátis

## Opção recomendada agora: Render Free

URL planejada da API:

```text
https://marketforge-api-d4rl1ng0.onrender.com
```

Se o Render gerar outro endereço, atualize a constante `DefaultApiBaseUrl` em:

```text
src/AlbionMarket.Mobile/MainPage.cs
```

## Passo a passo

1. Crie um repositório no GitHub, exemplo: `MarketForge`.
2. Suba esta pasta inteira para o GitHub.
3. Entre no Render.
4. New + → Blueprint ou Web Service.
5. Conecte o repositório.
6. Se usar Blueprint, o Render lerá `render.yaml`.
7. Se usar Web Service manual:
   - Runtime: Docker
   - Plan: Free
   - Dockerfile path: `./Dockerfile`
   - Health check path: `/health`
8. Depois do deploy, abra:

```text
https://SEU-SERVICO.onrender.com/health
```

Resposta esperada:

```json
{"status":"ok","app":"MarketForge API","version":"1.0.1"}
```

## Gerar app Android apontando para a API hospedada

Depois que a API estiver online, edite em `MainPage.cs`:

```csharp
private const string DefaultApiBaseUrl = "https://marketforge-api-d4rl1ng0.onrender.com";
```

Depois rode:

```powershell
dotnet build .\src\AlbionMarket.Mobile\AlbionMarket.Mobile.csproj -f net10.0-android -c Release "-p:JavaSdkDirectory=C:\Program Files\Android\Android Studio\jbr"
```

Para instalar/testar no emulador:

```powershell
dotnet build .\src\AlbionMarket.Mobile\AlbionMarket.Mobile.csproj -f net10.0-android -t:Run "-p:JavaSdkDirectory=C:\Program Files\Android\Android Studio\jbr"
```
