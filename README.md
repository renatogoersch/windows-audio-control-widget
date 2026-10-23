# Windows Audio Control – Widget

Widget minimalista (o programa se chama **SlotMixer**) para a barra de tarefas do Windows 11: uma caixa fina com ícone de headset e até 8 slots, à esquerda do tray/relógio. Cada slot guarda um app aberto (Chrome, Discord, Dota 2…) e, com um clique, troca a saída de áudio e o microfone **só daquele app**; a rolagem do mouse ajusta o volume dele. É um atalho para o "Mixer de volume" do Windows (Configurações › Som › Mixer de volume): saída, microfone e volume são os mesmos de lá, então aparecem lá também.

## Uso

| Ação | Resultado |
|---|---|
| Clique num slot vazio (`+`) | Lista os apps abertos (com áudio primeiro) para alocar no slot |
| Clique num slot com app | Lista as saídas de áudio **e os microfones**; clique numa opção para trocar (✓ = atual). Se o slot tem atalho, cada saída mostra um ícone ↻ (veja abaixo) |
| Rolar o mouse sobre o slot | Volume do app, de 5% em 5% |
| Passar o mouse | Tooltip: `App → saída`, microfone (se fixado), volume e atalho |
| **Slot do Windows** (logo azul, logo depois do headset) | Clique: trocar a saída e o microfone **padrão do Windows**. Rolagem: volume geral. Botão direito: atalhos |
| Clique no headset | Adiciona um slot (até 8) |
| Botão direito no slot | Só o que é do slot: trocar/escolher app, seção **Atalhos** (alternar saída, aumentar e diminuir volume, mudo), esvaziar, remover slot |
| Botão direito no headset | O menu do programa: adicionar slot, **perfis**, **ícones dos dispositivos**, **mover o widget**, iniciar com o Windows, instalar/desinstalar, sair |

O ícone pequeno no canto inferior direito do slot é o **tipo da saída** do app (fone, caixa de som, monitor), na cor do dispositivo; o do canto inferior esquerdo, um microfone, só aparece quando o app está fixado em um microfone diferente do padrão. Slot com ícone esmaecido = app fechado (a alocação é mantida).

### Atalhos por slot

Botão direito no slot › seção **Atalhos**: quatro ações, cada uma com a sua combinação à direita. Clique na linha e aperte a combinação (`Esc` cancela, `Backspace` remove). Exige Ctrl/Alt/Shift/Win (F13–F24 valem sozinhas) e recusa combinações já usadas por outro slot, perfil ou programa.

- **Alternar saída**: o app do slot passa para a próxima saída (ordem alfabética, volta ao início). O slot pisca com selo ✓ verde (trocou) ou ✕ vermelho (não deu: app fechado, sem áudio ainda, nenhuma outra saída marcada) e um aviso aparece no centro da tela com app, saída e posição (`2 de 3`).
- **Aumentar volume** / **Diminuir volume**: 5% por vez, e **segurar a tecla continua mudando**. O aviso mostra o volume com uma barra. Úteis em jogo, onde a barra de tarefas some. Aumentar o volume (pelo atalho ou pela rolagem do mouse) também tira o mudo.
- **Mudo**: liga e desliga o mudo do app (o mesmo mudo do Mixer de volume do Windows). Enquanto o app está mudo, o slot mostra um alto-falante vermelho cortado no canto superior esquerdo e o tooltip diz "Mudo".

**Quais saídas o alternar percorre**: com o slot tendo atalho, o clique normal no slot mostra um ícone ↻ ao lado de cada saída. Clicar no ícone, ou com o botão direito sobre a linha, tira a saída do ciclo (ícone apagado) ou a devolve. A saída fora do ciclo continua na lista para escolha manual; só o atalho a ignora. Trocar o app do slot mantém os atalhos e essa escolha.

### Slot do Windows

Slot fixo, que não se remove, logo depois do headset e separado dos slots de app por uma linha fina. É o ponto de controle do que o Windows usa **por padrão**: todo app que está em "Padrão do sistema" acompanha o que você escolher aqui, de uma vez.

- O ícone é o logo do Windows; o canto direito mostra o tipo e a cor da saída padrão atual, e a engrenagem do canto esquerdo marca que é o slot do sistema.
- **Clique**: lista as saídas e os microfones; clicar numa opção a torna o padrão do Windows. Com atalho definido, as saídas também mostram o ícone ↻ do ciclo.
- **Rolagem**: volume geral da saída padrão (5% por vez).
- **Botão direito › Atalhos**: *Alternar saída padrão*, *Aumentar volume geral*, *Diminuir volume geral* e *Mudo geral*, com o mesmo comportamento dos atalhos de um slot (segurar o volume repete).
- **Dispositivo de comunicação** (o que chamadas de voz e vídeo usam, que no Windows pode ser diferente do padrão): na primeira troca o SlotMixer decide, pela configuração da máquina, se o leva junto, e guarda a decisão. Quem o configurou à parte não tem o dispositivo arrastado pelo ciclo. O item *Trocar também o dispositivo de comunicação* do menu muda isso.
- Apps com saída própria fixada (nos slots) não são afetados pela troca do padrão.

### Perfis

Um perfil guarda, para cada app dos slots que estiver aberto, a saída e o microfone, **e o padrão do Windows** do momento (aplicado primeiro, para que os apps em "Padrão do sistema" o acompanhem). Botão direito no headset › **Salvar estado atual como perfil…** (dê um nome); clicar no perfil o aplica (✓ marca o perfil em vigor). **Gerenciar perfis…** permite aplicar, atualizar com o estado atual, definir/remover o atalho do perfil e excluir. Apps fechados na hora de aplicar são pulados e avisados.

### Posição na barra

Por padrão o widget fica colado à esquerda dos ícones do tray. Botão direito no headset › **Mover o widget na barra…**: o contorno do widget fica azul e ele segue o mouse **só para a esquerda e para a direita** (nunca sai da barra, nem sobe ou desce) enquanto você o arrasta; ao soltar, a posição é salva e o modo termina. Botão direito sobre o widget, ou 12 segundos sem tocá-lo, também saem do modo sem mover nada. **Voltar à posição original** (no mesmo menu, só aparece depois de mover) o cola de novo ao tray. A posição é a distância até o tray, em DIPs, então acompanha mudanças de resolução e escala; o limite à direita é o próprio tray, e à esquerda a borda da barra. Atenção: no Windows 11 os ícones dos apps ficam no centro da barra, e o widget passa por cima deles se for levado até lá.

### Ícones dos dispositivos

O Windows chama muitos headsets de "Speakers"; o SlotMixer corrige os mais comuns pelo nome (PRO X, Arctis, HyperX…). Para qualquer outro: botão direito no headset › **Ícones dos dispositivos…** e clique para trocar o tipo; a escolha fica salva.

### Instalação (opcional)

Na primeira abertura fora da pasta de instalação, o exe oferece instalar: copia-se para `%LOCALAPPDATA%\Programs\SlotMixer`, cria um atalho no Menu Iniciar e uma entrada em Configurações › Aplicativos instalados; não precisa de administrador. Linha de comando: `--install` (instala ou atualiza sem perguntar, mesmo com uma cópia rodando), `--uninstall` (`--quiet` sem perguntas, mantém as configurações), `--portable` (nunca oferece instalar). Abrir um exe mais novo com a versão instalada rodando oferece atualizar.

### Atualizações

Uma cópia **instalada** se atualiza sozinha: 45 segundos depois de abrir (e a cada 6 horas) ela pergunta ao GitHub qual é o último Release; se for mais novo, baixa o zip do mesmo tipo da cópia (leve ou autônomo), confere o tamanho e a soma SHA-256 que o GitHub informa e a versão do exe lá dentro, e o instala por cima. Isso só acontece quando você não está em jogo ou apresentando, sem menu aberto, e um aviso na tela aparece antes de o widget reiniciar; depois dele, "SlotMixer atualizado" confirma a nova versão. Configurações, slots e perfis ficam como estavam.

Botão direito no headset: **Verificar atualizações agora** (atualiza na hora, se houver), **Atualizar automaticamente** (liga/desliga) e, quando há versão nova pendente, **Atualizar agora**. A única coisa enviada é a consulta HTTPS à API pública do GitHub (com o agente `SlotMixer/versão`); nada do seu computador. Uma cópia não instalada nunca se substitui sozinha: ela só avisa e abre a página de download.

### Tema claro e escuro

O widget, os menus, o aviso e os tooltips seguem o tema da **barra de tarefas** do Windows (`SystemUsesLightTheme`) e acompanham a troca em poucos segundos, sem reiniciar. Cada tema tem a sua paleta de cores dos dispositivos, para que as cores continuem legíveis na barra clara.

## Compilar e rodar

```
cd SlotMixer
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Precisa do SDK do .NET 8 para compilar e do .NET 8 Desktop Runtime para rodar (Windows 11, x64). Configurações em `%APPDATA%\SlotMixer\settings.json`; erros em `error.log` na mesma pasta; `SLOTMIXER_TRACE=1` no ambiente grava um `trace.log` com o que cada atalho decide e por que uma consulta de atualização falhou; `SLOTMIXER_THEME=light` ou `dark` força um tema (para conferir as cores); `SLOTMIXER_UPDATE_FEED=<url>` troca o endereço da consulta de atualização (um servidor local nos testes, ou o de um fork).

## Enviar para outra pessoa

Os zips da versão atual estão na página de [Releases](https://github.com/renatogoersch/windows-audio-control-widget/releases) do repositório. São dois (cada um com `SlotMixer.exe` e um `LEIA-ME.txt` em português):

| Zip | Tamanho | Requisito |
|---|---|---|
| `SlotMixer-requer-dotnet.zip` | ~0,1 MB | .NET 8 Desktop Runtime (x64) instalado |
| `SlotMixer-autonomo.zip` | ~58 MB | nenhum (leva o .NET dentro) |

O exe não é assinado, então o SmartScreen avisa na primeira vez. Para montar os zips na sua máquina (vão para `dist\`, que não é versionada): `tools\build-zips.ps1`. A versão autônoma precisa dos pacotes `Microsoft.NETCore.App.Runtime.win-x64` e `Microsoft.WindowsDesktop.App.Runtime.win-x64` (8.0.x, ~35 MB cada, do NuGet; ficam no cache depois do primeiro uso).

## Lançar uma versão

Sem servidor próprio: o Release do GitHub é o canal de distribuição *e* o aviso de atualização das cópias instaladas.

1. Escreva o que mudou numa seção `## X.Y.Z` do [CHANGELOG.md](CHANGELOG.md); ela vira o texto do Release.
2. Com tudo commitado em `main`: `tools\release.ps1 -Version X.Y.Z`. Ele grava a `<Version>` no `.csproj`, faz o commit, cria a tag `vX.Y.Z` e a envia.
3. A tag dispara o workflow [Release](.github/workflows/release.yml) (GitHub Actions, de graça em repositório público): compila as duas versões, recusa a tag se ela não bater com o `.csproj`, monta os zips e publica o Release. (`-Local` faz o mesmo na sua máquina, com a CLI `gh`, sem esperar o Actions.)
4. Em até 6 horas as cópias instaladas veem o Release novo e se atualizam.

O número precisa subir sempre: é a `<Version>` que decide se a cópia instalada considera o Release mais novo. Um Release marcado como *pre-release* ou rascunho não é visto pelas cópias (a consulta é ao "latest"), o que serve para testar sem atingir ninguém.

## Como funciona

- **Troca por app**: `IAudioPolicyConfigFactory` (`Windows.Media.Internal.AudioPolicyConfig`), a API interna não documentada que o Windows usa no Mixer de volume e que o EarTrumpet também usa (`Audio/AppRouting.cs`), para saída e para microfone. Pode mudar entre versões do Windows; o IID usado é o do Windows 10 21H2 em diante (validado no build 26200).
- **Dispositivos, tipos e apps com áudio**: MMDevice (`PKEY_AudioEndpoint_FormFactor` dá o tipo) e `IAudioSessionManager2` via COM (`Audio/Endpoints.cs`).
- **Atalhos globais**: `RegisterHotKey` numa janela oculta (`HotkeyHost.cs`); o aviso na tela é uma janela sem foco e transparente ao mouse (`Osd.cs`), visível sobre jogos em janela sem borda, mas não sobre tela cheia exclusiva.
- **Volume por app**: `ISimpleAudioVolume` em todas as sessões de áudio do app (`Audio/AppVolume.cs`).
- **Padrão do Windows e volume geral**: `IPolicyConfig` (a interface por trás do painel de Som, a mesma que o EarTrumpet usa) para trocar o padrão e `IAudioEndpointVolume` para o volume geral (`Audio/SystemAudio.cs`). A lógica do slot está em `WidgetWindow.System.cs`.
- **Posição**: janela sem borda, sempre no topo e sem foco, posicionada sobre a barra logo à esquerda do `TrayNotifyWnd` e reposicionada a cada 250 ms. O Windows 11 não tem API oficial para widgets na barra.
- **Painéis da barra** (volume, rede, relógio, ocultos, Iniciar): ao abrir um deles o Windows leva a barra para uma camada acima de qualquer janela comum, até as "sempre no topo". Por isso a janela do widget tem a barra como *dona* (`GWLP_HWNDPARENT`), o que a mantém empilhada junto com ela e visível. Uma janela *filha* da barra foi descartada: o Windows 11 não desenha filhas com transparência por pixel sobre o conteúdo da barra. Se o Explorer for reiniciado, a janela some junto com a barra e o SlotMixer se reabre sozinho.
- **Instalador**: o próprio exe (`Installer.cs`), sem ferramentas externas.
- **Atualizador** (`Updater.cs`, `WidgetWindow.Update.cs`): `GET /repos/…/releases/latest` na API pública do GitHub, sem token. O zip é escolhido por um metadado gravado na compilação (`AssemblyMetadata SelfContained`), e o exe baixado, rodando em modo `--install`, é o mesmo caminho do instalador: para a cópia em execução, copia-se por cima e abre-se a nova.
- **Tema**: `Theme.cs` lê `SystemUsesLightTheme` a cada 2 s; `Refresh` reaplica as cores e `ApplyToolTipTheme` empurra o estilo novo aos tooltips (que ficam fora da árvore visual).

## Limitações conhecidas

- Monitores: o widget fica na barra do **monitor principal** (a que tem o tray e o relógio) e usa o tamanho e o DPI desse monitor, a cada 250 ms. Resolução, escala (100%/125%/150%/200%…), troca de monitor principal e barra de tarefas pequena são reajustadas sozinhas; a barra de monitores secundários não recebe o widget. Testado só em 1 monitor 5120×2160 a 150%.
- Só a barra na parte de baixo da tela (o único layout do Windows 11).
- O Windows só aceita trocar a saída de apps que já abriram áudio (e o microfone, de apps que já o usaram); para os outros o menu avisa.
- Perfis e atalhos só atuam em apps abertos; o Windows não permite fixar a saída de um app que não está rodando.
- Em jogo em tela cheia o widget some junto com a barra (os atalhos continuam valendo).
- Se muitos apps estiverem fixados/abertos na barra a ponto de chegar perto do tray, os ícones passam por baixo do widget.

## Licença

MIT, veja [LICENSE](LICENSE).

