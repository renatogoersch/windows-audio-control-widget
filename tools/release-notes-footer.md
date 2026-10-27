## Qual zip baixar / Which zip to download

| Arquivo / File | Tamanho / Size | Requisito / Requirement |
|---|---|---|
| `SlotMixer-requer-dotnet.zip` | ~0,1 MB | [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64) instalado / installed |
| `SlotMixer-autonomo.zip` | ~58 MB | nenhum (leva o .NET dentro) / none (carries .NET inside) |

Cada zip traz o `SlotMixer.exe`, um `LEIA-ME.txt` em português e um `README.txt` em inglês. Na primeira abertura o exe oferece instalar; quem já tem o SlotMixer instalado recebe esta versão sozinho.

Each zip has `SlotMixer.exe`, a `LEIA-ME.txt` in Portuguese and a `README.txt` in English. On the first run the exe offers to install; anyone who already has SlotMixer installed gets this version by itself. Also available through winget and Scoop (see `docs/distribution.md`).

## Avisos / Notes

- O exe **não é assinado**: o Windows SmartScreen avisa na primeira vez ("Mais informações" › "Executar assim mesmo"). / The exe is **not signed**: Windows SmartScreen warns the first time ("More info" › "Run anyway").
- Testado no Windows 11 (builds 26200 e 26300), com 3 monitores (125% a 300%). A troca de saída por app usa uma API interna do Windows, que pode mudar entre versões. / Tested on Windows 11 (builds 26200 and 26300), with 3 displays (125% to 300%). Per-app switching uses an internal Windows API, which can change between versions.