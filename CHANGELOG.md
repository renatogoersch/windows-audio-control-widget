# Changelog

Cada versão ganha uma seção `## X.Y.Z`; o texto dela vira o corpo do Release no GitHub (veja `tools/release-notes.ps1`).

## 1.7.1

**Português**

- **O sinal verde de som agora é um contorno no ícone de saída do slot** (o pequeno, no canto inferior direito), em vez de uma linha na borda de cima. **Correção:** ele aparecia em todos os apps que dividiam a mesma saída, e não só no que estava tocando (o medidor de sessão do Windows devolve o nível do dispositivo inteiro nesse caso). Agora cada app é medido sozinho, pela captura loopback por processo do Windows: um app calado com sessão ativa (o Discord, por exemplo) não acende.
- **Anel verde ao redor do slot** quando entra som pelo microfone daquele app (alguém falando). O círculo no canto continua dizendo que o app está com o microfone aberto; o anel diz que há som entrando. A sensibilidade é a configuração `InputThreshold` (padrão 0,05).

**English**

- **The green sound signal is now an outline on the slot's output icon** (the small one at the bottom right), instead of a line along the top edge. **Fix:** it showed on every app that shared the same output, not only on the one playing (Windows' session meter returns the whole device's level in that case). Each app is now measured on its own, through Windows' per-process loopback capture: a silent app with an active session (Discord, say) does not light up.
- **Green ring around the slot** when sound comes in through that app's microphone (somebody talking). The corner circle still says the app has the microphone open; the ring says sound is coming in. The sensitivity is the `InputThreshold` setting (default 0.05).
## 1.7.0

**Português**

- **Inglês.** O programa fala português ou inglês conforme o idioma do Windows (e dá para trocar em Ajuda e preferências). Os zips trazem também um `README.txt` em inglês, e o README do projeto passou a ser em inglês, com a versão em português em `README.pt-BR.md`.
- **Vários monitores.** Com mais de um monitor, o menu do headset ganha *Mostrar o widget*: só no principal ou uma cópia em cada barra de tarefas. O aviso na tela e os menus abrem no monitor onde você está, e não mais sempre no principal. *Testado com três monitores (150%, 300% e 125%); veja as limitações no README.*
- **Sem cobrir os ícones da barra.** O widget lê onde ficam os botões dos apps e encolhe (até 70%) para não ficar por cima deles.
- **Ajuda e preferências**, com *Copiar diagnóstico* e *Reportar um problema…* (o texto copiado traz o Windows, os monitores e as barras de tarefas, sem o nome do usuário), idioma e atualizações.
- **Clique do meio** no slot liga e desliga o mudo.
- **Círculo verde** no slot enquanto o app usa o microfone e **linha verde fina** enquanto ele toca som. O verde saiu da paleta de cores dos dispositivos para não haver confusão.
- **Mudo do microfone** no slot do Windows, com atalho e aviso.
- **Perfis automáticos**: um perfil pode se aplicar quando um app abre ou um dispositivo conecta, e voltar ao estado anterior ao terminar.
- **Equilíbrio jogo/chat**: três atalhos movem o volume entre dois apps, como o dial de um headset.
- **Comandos de fora**: `SlotMixer.exe --cycle 2`, `--mute windows`, `--mic-mute`, `--profile "Jogo"` e links `slotmixer://…`, para Stream Deck, AutoHotkey e scripts.
- O botão ↻ do ciclo de saídas aparece sempre, mesmo antes de o slot ter atalho.
- Winget e Scoop: manifestos e roteiro em `docs/distribution.md`; cópias instaladas por eles não oferecem instalar nem se atualizam sozinhas. Testes automáticos (CI) e um roteiro para assinar o exe de graça (`docs/signing.md`).

**English**

- **English.** The program speaks Portuguese or English following the Windows language (switchable in Help and preferences). The zips also carry an English `README.txt`, and the project README is now English, with Portuguese in `README.pt-BR.md`.
- **Multiple displays.** With more than one display, the headset menu gains *Show the widget*: the main taskbar only, or a copy on every taskbar. Messages and menus open on the display you are working on instead of always the main one. *Tested with three displays (150%, 300% and 125%); see the limitations in the README.*
- **Stays off the taskbar icons.** The widget reads where the app buttons are and shrinks (down to 70%) so as not to sit on top of them.
- **Help and preferences**, with *Copy diagnostics* and *Report a problem…* (the copied text has Windows, displays and taskbars, without the user name), language and updates.
- **Middle-click** on a slot toggles mute.
- **Green circle** on a slot while its app uses the microphone, and a **thin green line** while it plays sound. Green left the device color palette so nothing is confused.
- **Microphone mute** on the Windows slot, with shortcut and on-screen message.
- **Automatic profiles**: a profile can apply when an app opens or a device connects, and go back to the previous state when it ends.
- **Game/chat balance**: three shortcuts move the volume between two apps, like a headset dial.
- **Outside commands**: `SlotMixer.exe --cycle 2`, `--mute windows`, `--mic-mute`, `--profile "Game"` and `slotmixer://…` links, for Stream Deck, AutoHotkey and scripts.
- The ↻ switch of the output cycle is always shown, even before the slot has a shortcut.
- winget and Scoop manifests and a guide in `docs/distribution.md`; copies installed by them do not offer to install and do not update themselves. Automated tests (CI) and a guide for signing the exe for free (`docs/signing.md`).

## 1.6.1

- **O botão ↻ de cada saída agora aparece sempre** no menu do clique esquerdo (slots de app e slot do Windows). Antes ele só aparecia depois de definir o atalho "Alternar saída" do slot, e quem ainda não tinha atalho não achava onde escolher quais saídas o atalho percorre. A escolha vale mesmo antes de o atalho existir, e uma nota sob a lista explica o botão e diz onde definir o atalho.

## 1.6.0

- **Atualização automática.** Uma cópia instalada procura versões novas na página de Releases do projeto (a cada 6 horas), baixa o zip do mesmo tipo da cópia (leve ou autônoma), confere o tamanho e a soma SHA-256 e se reinstala quando você não está em jogo, com um aviso antes de reiniciar. Menu do headset: **Verificar atualizações agora** e **Atualizar automaticamente** (liga/desliga). Cópias não instaladas só avisam e abrem a página de download.
- **Tema claro e escuro.** As cores dos dispositivos ganharam uma paleta própria para a barra clara (as pastéis do tema escuro quase sumiam), o texto de erro do gravador de atalhos também, e os tooltips acompanham a troca de tema do Windows sem reiniciar o widget.
- Depois de uma atualização, o widget mostra "SlotMixer atualizado" uma vez.

## 1.5.0

Primeira versão pública.

- Até 8 slots de app: saída de áudio e microfone por app, volume por rolagem, mudo, ícones por tipo de dispositivo.
- **Slot do Windows** (logo azul): padrão do sistema, volume geral e mudo geral.
- Atalhos globais por slot (alternar saída, volume +/−, mudo), com aviso na tela.
- Perfis (apps e padrão do Windows), instalador e atualizador no próprio exe, mover o widget na barra.
- O widget continua visível com os painéis de volume, rede, relógio e Iniciar abertos.
