# Windows Audio Control – Widget

[English](README.md) · **Português (Brasil)**

Widget minimalista (o programa se chama **SlotMixer**) para a barra de tarefas do Windows 11: uma caixa fina com ícone de headset e até 8 slots, à esquerda do tray/relógio. Cada slot guarda um app aberto (Chrome, Discord, Dota 2…) e, com um clique, troca a saída de áudio e o microfone **só daquele app**; a rolagem do mouse ajusta o volume dele. É um atalho para o "Mixer de volume" do Windows (Configurações › Som › Mixer de volume): saída, microfone e volume são os mesmos de lá, então aparecem lá também.

O programa fala **português ou inglês**, conforme o idioma de exibição do Windows (dá para trocar no menu).

## Uso

| Ação | Resultado |
|---|---|
| Clique num slot vazio (`+`) | Lista os apps abertos (com áudio primeiro) para alocar no slot |
| Clique num slot com app | Lista as saídas de áudio **e os microfones**; clique numa opção para trocar (✓ = atual). Cada saída tem um ícone ↻ (veja abaixo) |
| Rolar o mouse sobre o slot | Volume do app, de 5% em 5% |
| Clique do meio no slot | Liga e desliga o mudo do app (no slot do Windows, o mudo geral) |
| Passar o mouse | Tooltip: `App → saída`, microfone (se fixado ou em uso), volume e atalhos |
| **Slot do Windows** (logo azul, logo depois do headset) | Clique: trocar a saída e o microfone **padrão do Windows**. Rolagem: volume geral. Botão direito: atalhos |
| Clique no headset | Adiciona um slot (até 8) |
| Botão direito no slot | Só o que é do slot: trocar/escolher app, seção **Atalhos** (alternar saída, aumentar e diminuir volume, mudo), esvaziar, remover slot |
| Botão direito no headset | O menu do programa: adicionar slot, **perfis**, **ícones dos dispositivos**, **equilíbrio jogo/chat**, **mover o widget**, **ajuda e preferências** (idioma, atualizações, diagnóstico), iniciar com o Windows, instalar/desinstalar, sair |

O ícone pequeno no canto inferior direito do slot é o **tipo da saída** do app (fone, caixa de som, monitor), na cor do dispositivo; o do canto inferior esquerdo, um microfone, só aparece quando o app está fixado em um microfone diferente do padrão. Slot com ícone esmaecido = app fechado (a alocação é mantida).

Três sinais "ao vivo", sempre em **verde** (a cor dos dispositivos nunca é verde, para não haver confusão):

- **Círculo verde no canto superior direito**: o app está **usando o microfone** agora (como o ponto de privacidade dos celulares).
- **Anel verde ao redor do slot**: está **entrando som** pelo microfone daquele app (alguém falando). Fica mais forte com o volume e some alguns instantes depois do silêncio. A sensibilidade é a configuração `InputThreshold` do `settings.json` (padrão `0.05`; suba se um microfone ruidoso mantiver o anel aceso, desça se falar baixo não o acender).
- **Linha verde fina na borda de cima**: o app está **tocando som**; ela cresce com o volume. Desliga em Ajuda e preferências › *Indicador de som nos slots*.

### Atalhos por slot

Botão direito no slot › seção **Atalhos**: quatro ações, cada uma com a sua combinação à direita. Clique na linha e aperte a combinação (`Esc` cancela, `Backspace` remove). Exige Ctrl/Alt/Shift/Win (F13–F24 valem sozinhas) e recusa combinações já usadas por outro slot, perfil ou programa.

- **Alternar saída**: o app do slot passa para a próxima saída (ordem alfabética, volta ao início). O slot pisca com selo ✓ verde (trocou) ou ✕ vermelho (não deu: app fechado, sem áudio ainda, nenhuma outra saída marcada) e um aviso aparece na tela com app, saída e posição (`2 de 3`).
- **Aumentar volume** / **Diminuir volume**: 5% por vez, e **segurar a tecla continua mudando**. O aviso mostra o volume com uma barra. Úteis em jogo, onde a barra de tarefas some. Aumentar o volume (pelo atalho ou pela rolagem do mouse) também tira o mudo.
- **Mudo**: liga e desliga o mudo do app (o mesmo mudo do Mixer de volume do Windows). Enquanto o app está mudo, o slot mostra um alto-falante vermelho cortado no canto superior esquerdo e o tooltip diz "Mudo".

**Quais saídas o alternar percorre**: cada saída da lista do clique normal tem um ícone ↻ à direita (aparece mesmo antes de existir o atalho). Clicar nele, ou com o botão direito sobre a linha, tira a saída do ciclo (ícone apagado) ou a devolve. A saída fora do ciclo continua na lista para escolha manual; só o atalho a ignora. Trocar o app do slot mantém os atalhos e essa escolha.

### Slot do Windows

Slot fixo, que não se remove, logo depois do headset e separado dos slots de app por uma linha fina. É o ponto de controle do que o Windows usa **por padrão**: todo app que está em "Padrão do sistema" acompanha o que você escolher aqui, de uma vez.

- O ícone é o logo do Windows; o canto direito mostra o tipo e a cor da saída padrão atual, e a engrenagem do canto esquerdo marca que é o slot do sistema (vira um microfone vermelho enquanto o microfone está mudo).
- **Clique**: lista as saídas e os microfones; clicar numa opção a torna o padrão do Windows.
- **Rolagem**: volume geral da saída padrão (5% por vez). **Clique do meio**: mudo geral.
- **Botão direito › Atalhos**: *Alternar saída padrão*, *Aumentar volume geral*, *Diminuir volume geral*, *Mudo geral* e *Mudo do microfone* (liga e desliga o mudo do microfone padrão, com aviso na tela), com o mesmo comportamento dos atalhos de um slot (segurar o volume repete).
- **Dispositivo de comunicação** (o que chamadas de voz e vídeo usam, que no Windows pode ser diferente do padrão): na primeira troca o SlotMixer decide, pela configuração da máquina, se o leva junto, e guarda a decisão. Quem o configurou à parte não tem o dispositivo arrastado pelo ciclo. O item *Trocar também o dispositivo de comunicação* do menu muda isso.
- Apps com saída própria fixada (nos slots) não são afetados pela troca do padrão.

### Perfis

Um perfil guarda, para cada app dos slots que estiver aberto, a saída e o microfone, **e o padrão do Windows** do momento (aplicado primeiro, para que os apps em "Padrão do sistema" o acompanhem). Botão direito no headset › **Salvar estado atual como perfil…** (dê um nome); clicar no perfil o aplica (✓ marca o perfil em vigor). **Gerenciar perfis…** permite aplicar, atualizar com o estado atual, definir/remover o atalho do perfil e excluir. Apps fechados na hora de aplicar são pulados e avisados.

**Perfil automático**: em *Gerenciar perfis…* › o perfil › **Quando aplicar**, o perfil pode se aplicar sozinho **quando um app abre** (por exemplo, o Dota 2) ou **quando um dispositivo conecta** (por exemplo, o headset). Só a mudança conta: um app que já estava aberto quando o SlotMixer iniciou não dispara nada. *Voltar ao estado anterior ao terminar* (ligado por padrão) devolve tudo como estava quando o app fecha ou o dispositivo sai. Se o app acabou de abrir e ainda não abriu áudio (o Windows só roteia quem já abriu), o SlotMixer tenta de novo por até 90 segundos.

### Equilíbrio jogo/chat

Botão direito no headset › **Equilíbrio jogo/chat…**: escolha o app do jogo e o do chat e defina até três atalhos (*mais jogo*, *mais chat*, *centralizar*). Cada aperto desloca o "dial" 10%: mais chat abaixa o jogo, mais jogo abaixa o chat, e o aviso mostra os dois volumes e a posição do dial. O volume é escalado a partir do que era quando o dial saiu do meio, então *centralizar* devolve os dois exatamente.

### Posição na barra

Por padrão o widget fica colado à esquerda dos ícones do tray. Botão direito no headset › **Mover o widget na barra…**: o contorno do widget fica azul e ele segue o mouse **só para a esquerda e para a direita** (nunca sai da barra, nem sobe ou desce) enquanto você o arrasta; ao soltar, a posição é salva e o modo termina. Botão direito sobre o widget, ou 12 segundos sem tocá-lo, também saem do modo sem mover nada. **Voltar à posição original** (no mesmo menu, só aparece depois de mover) o cola de novo ao tray. A posição é a distância até o tray, em DIPs, então acompanha mudanças de resolução e escala.

**Sem cobrir os ícones**: enquanto o widget está colado ao tray, ele lê onde ficam os botões dos apps (pela Automação da Interface do Windows) e **encolhe até 70%** para não ficar por cima deles. Um widget que você mesmo moveu fica onde você pôs.

### Vários monitores

O widget fica por padrão só na barra do monitor principal. Com mais de um monitor, o menu do headset ganha **Mostrar o widget: só no monitor principal / em todos os monitores**: com a segunda opção, cada barra de tarefas ganha o seu widget (ative "Mostrar minha barra de tarefas em todos os monitores" no Windows), todos mostrando os mesmos slots, com a escala (DPI) de cada monitor. O aviso na tela e os menus abrem **no monitor onde você está** (o da janela ativa, ou do mouse), não no principal.

> **Testado** num PC com **três monitores**: o principal 5120×2160 a 150%, um 4K a **300%** e um vertical (portrait) a **125%**, cada um com a sua barra. Funcionaram: o widget em cada barra na escala certa, o menu e o aviso no monitor ativo, e a barra estreita do vertical (onde o widget encolhe para não cobrir os ícones). A lógica de posição também tem testes automáticos. Arranjos que não foram vistos (barra oculta automaticamente em um dos monitores, monitores acima do principal, mais de três) podem ter surpresas: use **Copiar diagnóstico** e abra uma issue.

### Ícones dos dispositivos

O Windows chama muitos headsets de "Speakers"; o SlotMixer corrige os mais comuns pelo nome (PRO X, Arctis, HyperX…). Para qualquer outro: botão direito no headset › **Ícones dos dispositivos…** e clique para trocar o tipo; a escolha fica salva.

### Controle de fora (Stream Deck, AutoHotkey, scripts)

O SlotMixer em execução aceita ordens da linha de comando e de links `slotmixer://` (registrado na instalação). Elas fazem o que o atalho de teclado faz, e só isso (não instalam, não saem, não mexem em configurações):

```
SlotMixer.exe --cycle 2             alternar a saída do slot 2          slotmixer://cycle/2
SlotMixer.exe --volume-up windows   volume geral +5%                    slotmixer://volume-up/windows
SlotMixer.exe --volume-down 1       volume do slot 1 −5%                slotmixer://volume-down/1
SlotMixer.exe --mute 3              mudo do slot 3                      slotmixer://mute/3
SlotMixer.exe --mic-mute            mudo do microfone                   slotmixer://mic-mute
SlotMixer.exe --profile "Jogo"      aplicar um perfil                   slotmixer://profile/Jogo
```

O slot é um número de 1 a 8 ou `windows`. Código de saída: 0 feito, 2 o SlotMixer não está rodando, 3 ordem inválida (com a explicação no terminal). A ordem vai por um *pipe* que só o mesmo usuário abre.

### Idioma

Português quando o Windows está em português, inglês nos demais casos. Ajuda e preferências › **Idioma** alterna entre automático, Português e English. `SLOTMIXER_LANG=pt` ou `en` no ambiente vale mais que tudo.

### Suporte

Ajuda e preferências › **Copiar diagnóstico** copia para a área de transferência um texto com a versão do Windows, os monitores e escalas, as barras de tarefas, o estado do widget e o fim do `error.log` (o nome de usuário é apagado). **Reportar um problema…** copia o mesmo texto e abre o formulário de issue do projeto, que pede para colá-lo. Nada é enviado sozinho.

### Instalação (opcional)

Na primeira abertura fora da pasta de instalação, o exe oferece instalar: copia-se para `%LOCALAPPDATA%\Programs\SlotMixer`, cria um atalho no Menu Iniciar, registra os links `slotmixer://` e uma entrada em Configurações › Aplicativos instalados; não precisa de administrador. Linha de comando: `--install` (instala ou atualiza sem perguntar, mesmo com uma cópia rodando), `--uninstall` (`--quiet` sem perguntas, mantém as configurações), `--portable` (nunca oferece instalar). Abrir um exe mais novo com a versão instalada rodando oferece atualizar. Cópias instaladas pelo **winget** ou pelo **Scoop** não oferecem instalar nem se atualizam sozinhas: quem atualiza é o gerenciador (veja [docs/distribution.md](docs/distribution.md)).

### Atualizações

Uma cópia **instalada** se atualiza sozinha: 45 segundos depois de abrir (e a cada 6 horas) ela pergunta ao GitHub qual é o último Release; se for mais novo, baixa o zip do mesmo tipo da cópia (leve ou autônomo), confere o tamanho e a soma SHA-256 que o GitHub informa e a versão do exe lá dentro, e o instala por cima. Isso só acontece quando você não está em jogo ou apresentando, sem menu aberto, e um aviso na tela aparece antes de o widget reiniciar; depois dele, "SlotMixer atualizado" confirma a nova versão. Configurações, slots e perfis ficam como estavam.

Ajuda e preferências: **Verificar atualizações agora** (atualiza na hora, se houver) e **Atualizar automaticamente** (liga/desliga); quando há versão nova pendente, o menu do headset ganha **Atualizar agora**. A única coisa enviada é a consulta HTTPS à API pública do GitHub (com o agente `SlotMixer/versão`); nada do seu computador. Uma cópia não instalada nunca se substitui sozinha: ela só avisa e abre a página de download.

### Tema claro e escuro

O widget, os menus, o aviso e os tooltips seguem o tema da **barra de tarefas** do Windows (`SystemUsesLightTheme`) e acompanham a troca em poucos segundos, sem reiniciar. Cada tema tem a sua paleta de cores dos dispositivos, para que as cores continuem legíveis na barra clara; o verde fica reservado aos sinais ao vivo.

## Compilar, testar e rodar

```
cd SlotMixer
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
cd ..
dotnet test tests/SlotMixer.Tests -c Release
```

Precisa do SDK do .NET 8 para compilar e do .NET 8 Desktop Runtime para rodar (Windows 11, x64). Os testes (xUnit) cobrem a geometria de posição, os comandos, o ciclo de saídas, a leitura do feed de atualização, os padrões das configurações e a **cobertura das traduções** (todo texto do código precisa ter a versão em inglês). Rodam a cada push no [CI](.github/workflows/ci.yml).

Configurações em `%APPDATA%\SlotMixer\settings.json`; erros em `error.log` na mesma pasta. Variáveis de ambiente: `SLOTMIXER_TRACE=1` grava um `trace.log` com o que cada atalho decide e por que uma consulta de atualização falhou; `SLOTMIXER_THEME=light` ou `dark` força um tema; `SLOTMIXER_LANG=pt` ou `en` força o idioma; `SLOTMIXER_UPDATE_FEED=<url>` troca o endereço da consulta de atualização (um servidor local nos testes, ou o de um fork).

## Enviar para outra pessoa

Os zips da versão atual estão na página de [Releases](https://github.com/renatogoersch/windows-audio-control-widget/releases) do repositório. São dois (cada um com `SlotMixer.exe`, um `LEIA-ME.txt` em português e um `README.txt` em inglês):

| Zip | Tamanho | Requisito |
|---|---|---|
| `SlotMixer-requer-dotnet.zip` | ~0,1 MB | .NET 8 Desktop Runtime (x64) instalado |
| `SlotMixer-autonomo.zip` | ~58 MB | nenhum (leva o .NET dentro) |

O exe não é assinado, então o SmartScreen avisa na primeira vez (o caminho para assinar de graça está em [docs/signing.md](docs/signing.md)). Para montar os zips na sua máquina (vão para `dist\`, que não é versionada): `tools\build-zips.ps1`. A versão autônoma precisa dos pacotes `Microsoft.NETCore.App.Runtime.win-x64` e `Microsoft.WindowsDesktop.App.Runtime.win-x64` (8.0.x, ~35 MB cada, do NuGet; ficam no cache depois do primeiro uso). Winget, Scoop e assinatura: [docs/distribution.md](docs/distribution.md).

## Lançar uma versão

Sem servidor próprio: o Release do GitHub é o canal de distribuição *e* o aviso de atualização das cópias instaladas.

1. Escreva o que mudou numa seção `## X.Y.Z` do [CHANGELOG.md](CHANGELOG.md); ela vira o texto do Release.
2. Com tudo commitado em `main`: `tools\release.ps1 -Version X.Y.Z`. Ele grava a `<Version>` no `.csproj`, faz o commit, cria a tag `vX.Y.Z` e a envia.
3. A tag dispara o workflow [Release](.github/workflows/release.yml) (GitHub Actions, de graça em repositório público): compila as duas versões, recusa a tag se ela não bater com o `.csproj`, monta os zips e publica o Release. (`-Local` faz o mesmo na sua máquina, com a CLI `gh`, sem esperar o Actions.)
4. Em até 6 horas as cópias instaladas veem o Release novo e se atualizam.

O número precisa subir sempre: é a `<Version>` que decide se a cópia instalada considera o Release mais novo. Um Release marcado como *pre-release* ou rascunho não é visto pelas cópias (a consulta é ao "latest"), o que serve para testar sem atingir ninguém.

## Como funciona

- **Troca por app**: `IAudioPolicyConfigFactory` (`Windows.Media.Internal.AudioPolicyConfig`), a API interna não documentada que o Windows usa no Mixer de volume e que o EarTrumpet também usa (`Audio/AppRouting.cs`), para saída e para microfone. Pode mudar entre versões do Windows; o IID usado é o do Windows 10 21H2 em diante (validado nos builds 26200 e 26300).
- **Dispositivos, tipos e apps com áudio**: MMDevice (`PKEY_AudioEndpoint_FormFactor` dá o tipo) e `IAudioSessionManager2` via COM (`Audio/Endpoints.cs`). O app que **usa o microfone** é o que tem uma sessão ativa num dispositivo de captura.
- **Atalhos globais**: `RegisterHotKey` numa janela oculta (`HotkeyHost.cs`); o aviso na tela é uma janela sem foco e transparente ao mouse (`Osd.cs`), visível sobre jogos em janela sem borda, mas não sobre tela cheia exclusiva.
- **Volume, mudo e nível por app**: `ISimpleAudioVolume` e `IAudioMeterInformation` nas sessões do app (`Audio/AppVolume.cs`). Os medidores são guardados e relidos a cada 200 ms (achá-los percorre todos os dispositivos, então só acontece a cada refresh); a linha de som usa 5 degraus porque uma janela em camadas é redesenhada inteira a cada mudança.
- **Padrão do Windows, volume geral e microfone**: `IPolicyConfig` (a interface por trás do painel de Som, a mesma que o EarTrumpet usa) para trocar o padrão e `IAudioEndpointVolume` para o volume geral e o mudo do microfone (`Audio/SystemAudio.cs`). A lógica do slot está em `WidgetWindow.System.cs`.
- **Posição**: janela sem borda, sempre no topo e sem foco, posicionada sobre a barra logo à esquerda do tray e reposicionada a cada 250 ms. A conta está em `Layout.cs` (funções puras, com testes). Os botões dos apps e o relógio são lidos pela Automação da Interface (`TaskbarProbe.cs`, numa thread de trabalho, com cache), porque a barra do Windows 11 é desenhada em XAML e não tem janelas para medir. Cada barra de tarefas ganha o seu widget (`WidgetWindow` com `_taskbarHwnd`); só o principal registra atalhos, atualiza e cria os outros.
- **Painéis da barra** (volume, rede, relógio, ocultos, Iniciar): ao abrir um deles o Windows leva a barra para uma camada acima de qualquer janela comum, até as "sempre no topo". Por isso a janela do widget tem a barra como *dona* (`GWLP_HWNDPARENT`), o que a mantém empilhada junto com ela e visível. Uma janela *filha* da barra foi descartada: o Windows 11 não desenha filhas com transparência por pixel sobre o conteúdo da barra. Se o Explorer for reiniciado, a janela some junto com a barra e o SlotMixer se reabre sozinho.
- **Monitores** (`Monitors.cs`): `EnumDisplayMonitors`, `GetMonitorInfo` e `GetDpiForMonitor`. Menus e avisos usam coordenadas em pixels físicos (`SetWindowPos`), para não dependerem do DPI do monitor de destino.
- **Instalador**: o próprio exe (`Installer.cs`), sem ferramentas externas.
- **Atualizador** (`Updater.cs`, `WidgetWindow.Update.cs`): `GET /repos/…/releases/latest` na API pública do GitHub, sem token. O zip é escolhido por um metadado gravado na compilação (`AssemblyMetadata SelfContained`), e o exe baixado, rodando em modo `--install`, é o mesmo caminho do instalador: para a cópia em execução, copia-se por cima e abre-se a nova.
- **Comandos de fora** (`Commands.cs`): um *named pipe* com ACL só para o usuário atual; o segundo processo envia a linha e sai.
- **Idioma** (`Loc.cs`, `Strings.En.cs`): o texto em português é a chave: `L("Texto")` devolve ele mesmo em português e procura a tradução em inglês nas demais línguas.
- **Tema**: `Theme.cs` lê `SystemUsesLightTheme` a cada 2 s; `Refresh` reaplica as cores e `ApplyToolTipTheme` empurra o estilo novo aos tooltips (que ficam fora da árvore visual).

## Limitações conhecidas

- **Monitores**: testado com três monitores (150%, 300% e 125%) no Windows 11 (builds 26200 e 26300). Barra de tarefas oculta automaticamente em monitores extras, monitores acima do principal e mais de três monitores não foram vistos; a lógica de posição é coberta por testes.
- **Windows 10**: não testado. A troca por app usa uma API interna cujo identificador só vale a partir do 21H2, e o widget supõe a barra embaixo (no Windows 10 ela pode ficar em cima ou nas laterais).
- **Windows ARM64**: só existe a versão x64 (roda por emulação).
- O Windows só aceita trocar a saída de apps que já abriram áudio (e o microfone, de apps que já o usaram); para os outros o menu avisa.
- Perfis e atalhos só atuam em apps abertos; o Windows não permite fixar a saída de um app que não está rodando.
- Em jogo em tela cheia o widget some junto com a barra (os atalhos continuam valendo).
- O exe não é assinado.

## Licença

MIT, veja [LICENSE](LICENSE).
