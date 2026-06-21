# AlbionMarket.Mobile

App .NET MAUI simples para Android, consumindo a API `AlbionMarket.Api`.

## Rodar API para celular físico

Na pasta raiz do projeto:

```powershell
dotnet run --project .\src\AlbionMarket.Api\AlbionMarket.Api.csproj --launch-profile phone
```

Depois descubra o IP do PC:

```powershell
ipconfig
```

Pegue o IPv4 da sua rede Wi-Fi, por exemplo `192.168.0.10`.
No app, coloque:

```text
http://192.168.0.10:5164
```

O celular e o PC precisam estar na mesma rede. Se o Windows Firewall perguntar, permita rede privada.

## Rodar o app Android

Com o celular conectado por USB e depuração USB ativada:

```powershell
dotnet build .\src\AlbionMarket.Mobile\AlbionMarket.Mobile.csproj -f net10.0-android
```

```powershell
dotnet build .\src\AlbionMarket.Mobile\AlbionMarket.Mobile.csproj -f net10.0-android -t:Run
```

Se o workload MAUI não estiver instalado:

```powershell
dotnet workload install maui-android
```
