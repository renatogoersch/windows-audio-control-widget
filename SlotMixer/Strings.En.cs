namespace SlotMixer;

internal static partial class Strings
{
    /// <summary>
    /// English texts, keyed by the Portuguese text used in the code (see <see cref="Loc"/>). Placeholders {0}, {1}… keep
    /// the same meaning in both languages, and a test checks that every <c>L("…")</c> in the sources has an entry here.
    /// </summary>
    public static readonly Dictionary<string, string> English = new()
    {
        // device kinds
        ["Fone de ouvido"] = "Headphones",
        ["Headset"] = "Headset",
        ["Caixas de som"] = "Speakers",
        ["Monitor / TV"] = "Monitor / TV",
        ["Microfone"] = "Microphone",
        ["Outro"] = "Other",

        // menu rows and tooltips
        ["Esta saída entra no ciclo do atalho. Clique para tirar."] = "This output is part of the shortcut's cycle. Click to remove it.",
        ["Esta saída fica fora do ciclo do atalho. Clique para incluir."] = "This output is left out of the shortcut's cycle. Click to include it.",
        ["Botão direito: menu do SlotMixer"] = "Right-click: SlotMixer menu",
        ["Clique: adicionar slot\n{0}"] = "Click: add a slot\n{0}",
        ["Limite de {0} slots\n{1}"] = "Limit of {0} slots\n{1}",

        // installer
        ["Instalar o SlotMixer neste computador?\n\nSim: copia o programa para a sua pasta de usuário (não precisa de administrador), cria um atalho no Menu Iniciar e abre o widget.\nNão: só executa a partir desta pasta, sem instalar (e não pergunta de novo).\nCancelar: sai.\n\nVocê pode instalar depois pelo menu do headset."] =
            "Install SlotMixer on this computer?\n\nYes: copies the program to your user folder (no administrator needed), adds a Start menu shortcut and opens the widget.\nNo: just runs from this folder without installing (and does not ask again).\nCancel: exits.\n\nYou can install later from the headset menu.",
        ["Não foi possível instalar:\n\n{0}"] = "Could not install:\n\n{0}",
        ["O SlotMixer {0} já está instalado e em execução.\n\nAtualizar para a versão {1}?"] = "SlotMixer {0} is already installed and running.\n\nUpdate to version {1}?",
        ["Desinstalar o SlotMixer?"] = "Uninstall SlotMixer?",
        ["Apagar também os seus slots, atalhos e perfis?\n\nSim: apaga tudo.\nNão: mantém as configurações (úteis se você reinstalar)."] =
            "Also delete your slots, shortcuts and profiles?\n\nYes: deletes everything.\nNo: keeps the settings (handy if you reinstall).",
        ["SlotMixer desinstalado."] = "SlotMixer uninstalled.",
        ["Não foi possível desinstalar tudo:\n\n{0}"] = "Could not uninstall everything:\n\n{0}",
        ["SlotMixer: saída de áudio por app na barra de tarefas"] = "SlotMixer: per-app audio output on the taskbar",

        // slot tooltip
        ["Slot vazio — clique para escolher um app"] = "Empty slot — click to choose an app",
        ["Microfone: {0}"] = "Microphone: {0}",
        ["Mudo  ·  volume {0}%"] = "Muted  ·  volume {0}%",
        ["Mudo"] = "Muted",
        ["Volume {0}%  ·  role o mouse para ajustar"] = "Volume {0}%  ·  scroll the mouse wheel to adjust",
        ["Alternar saída: {0}"] = "Switch output: {0}",
        ["Volume +: {0}   ·   Volume −: {1}"] = "Volume +: {0}   ·   Volume −: {1}",
        ["Mudo: {0}"] = "Mute: {0}",

        // slot menus
        ["{0} — fechado"] = "{0} — closed",
        ["dispositivo desconhecido"] = "unknown device",
        [" (padrão do sistema)"] = " (system default)",
        ["Saída de áudio e microfone"] = "Audio output and microphone",
        ["Saída de áudio"] = "Audio output",
        ["Padrão do sistema"] = "System default",
        ["Slot {0}"] = "Slot {0}",
        ["Escolha um app que esteja aberto"] = "Choose an app that is open",
        ["Com áudio"] = "With audio",
        ["Outros apps abertos"] = "Other open apps",
        ["Nenhum app aberto encontrado"] = "No open apps found",
        ["Slot {0} — app fechado"] = "Slot {0} — app closed",
        ["Trocar app…"] = "Change app…",
        ["Atalhos"] = "Shortcuts",
        ["definir…"] = "set…",
        ["Esvaziar slot"] = "Empty slot",
        ["Vazio"] = "Empty",
        ["Escolher app…"] = "Choose app…",
        ["Remover slot"] = "Remove slot",
        ["↻ à direita: a saída entra no ciclo do atalho \"Alternar saída\". Clique no ícone, ou no botão direito sobre a linha, para marcar ou tirar."] =
            "↻ on the right: the output is part of the \"Switch output\" shortcut's cycle. Click the icon, or right-click the row, to include or remove it.",
        [" Este slot ainda não tem o atalho: botão direito no slot › Atalhos."] = " This slot has no shortcut yet: right-click the slot › Shortcuts.",

        // headset menu
        [" (autônoma)"] = " (self-contained)",
        ["  ·  nova: {0}"] = "  ·  new: {0}",
        ["{0} de {1} slots  ·  versão {2}"] = "{0} of {1} slots  ·  version {2}",
        ["Adicionar slot"] = "Add slot",
        ["Perfis"] = "Profiles",
        ["Salvar estado atual como perfil…"] = "Save current state as a profile…",
        ["Novo perfil"] = "New profile",
        ["Gerenciar perfis…"] = "Manage profiles…",
        ["Ícones dos dispositivos…"] = "Device icons…",
        ["Mover o widget na barra…"] = "Move the widget on the taskbar…",
        ["Voltar à posição original"] = "Back to the original position",
        ["Ícones dos dispositivos"] = "Device icons",
        ["Clique para trocar o tipo do ícone"] = "Click to change the icon type",
        ["Saídas"] = "Outputs",
        ["Microfones"] = "Microphones",
        ["Iniciar com o Windows"] = "Start with Windows",
        ["Desinstalar…"] = "Uninstall…",
        ["Instalar o SlotMixer…"] = "Install SlotMixer…",
        ["Sair"] = "Exit",
        ["Idioma: {0}"] = "Language: {0}",
        ["Automático (Windows)"] = "Automatic (Windows)",

        // notices and profiles
        ["Este app ainda não abriu nenhum áudio. Toque algo nele e tente de novo."] = "This app has not opened any audio yet. Play something in it and try again.",
        ["Este app ainda não usou o microfone. Use o microfone nele (por exemplo, entrando numa chamada) e tente de novo."] = "This app has not used the microphone yet. Use the microphone in it (for example by joining a call) and try again.",
        ["Nenhum app dos slots está aberto, não há o que salvar"] = "None of the slot apps is open, so there is nothing to save",
        ["Perfil salvo com {0} app(s) e o padrão do Windows"] = "Profile saved with {0} app(s) and the Windows default",
        ["Perfil excluído"] = "Profile deleted",
        ["padrão do Windows  ·  "] = "Windows default  ·  ",
        ["{0} app(s) ajustado(s)"] = "{0} app(s) adjusted",
        ["  ·  {0} fechado(s)"] = "  ·  {0} closed",
        ["  ·  sem áudio ainda: {0}"] = "  ·  no audio yet: {0}",
        ["Perfil: {0}"] = "Profile: {0}",
        ["saída padrão"] = "default output",
        ["saída indisponível"] = "output unavailable",
        ["  ·  mic: {0}"] = "  ·  mic: {0}",
        ["indisponível"] = "unavailable",
        ["Enter salva  ·  Esc cancela"] = "Enter saves  ·  Esc cancels",
        ["Dê um nome (por exemplo: Jogo, Reunião, Música)"] = "Give it a name (for example: Game, Meeting, Music)",
        ["Escolha um para gerenciar"] = "Pick one to manage",
        ["{0} app(s)"] = "{0} app(s)",
        ["Aplicar agora"] = "Apply now",
        ["Atualizar com o estado atual"] = "Update with the current state",
        ["Atalho: {0}  ·  alterar…"] = "Shortcut: {0}  ·  change…",
        ["Definir atalho…"] = "Set shortcut…",
        ["Remover atalho"] = "Remove shortcut",
        ["Excluir perfil"] = "Delete profile",
        ["O que este perfil faz"] = "What this profile does",

        // shortcut actions and feedback
        ["Atalho {0} indisponível: já usado por outro programa"] = "Shortcut {0} unavailable: already used by another program",
        ["Aumentar volume"] = "Volume up",
        ["Diminuir volume"] = "Volume down",
        ["Alternar saída"] = "Switch output",
        ["App fechado"] = "App closed",
        ["Este app ainda não abriu nenhum áudio"] = "This app has not opened any audio yet",
        ["Volume {0}%"] = "Volume {0}%",
        ["Som ativado"] = "Sound on",
        ["Não foi possível ler as saídas de áudio"] = "Could not read the audio outputs",
        ["Nenhuma saída marcada para o atalho deste slot"] = "No output is marked for this slot's shortcut",
        ["Só há uma saída marcada para o atalho deste slot"] = "Only one output is marked for this slot's shortcut",
        ["{0}  ·  {1} de {2}"] = "{0}  ·  {1} of {2}",
        ["{0}: atalho removido"] = "{0}: shortcut removed",
        ["Atalho removido"] = "Shortcut removed",
        ["Atalho definido: {0}"] = "Shortcut set: {0}",
        ["perfil {0}"] = "profile {0}",
        ["Atalho: {0}"] = "Shortcut: {0}",
        ["Atalho para aplicar este perfil"] = "Shortcut to apply this profile",
        ["Nenhum"] = "None",
        ["Pressione a combinação desejada.\nEsc cancela  ·  Backspace remove o atalho."] = "Press the combination you want.\nEsc cancels  ·  Backspace removes the shortcut.",
        ["Use Ctrl, Alt, Shift ou Win junto com outra tecla."] = "Use Ctrl, Alt, Shift or Win together with another key.",
        ["Esse atalho já é do {0}."] = "That shortcut already belongs to the {0}.",
        ["Esse atalho já está em uso por outro programa."] = "That shortcut is already in use by another program.",

        // move mode
        ["Arraste para a esquerda ou a direita\nBotão direito: sair"] = "Drag left or right\nRight-click: exit",
        ["Mover o widget"] = "Move the widget",
        ["Arraste para a esquerda ou a direita. Botão direito sai."] = "Drag left or right. Right-click exits.",

        // Windows slot
        ["Padrão do Windows"] = "Windows default",
        ["Vale para todo app que está em \"Padrão do sistema\""] = "Applies to every app set to \"System default\"",
        ["Saída de áudio padrão"] = "Default audio output",
        ["Microfone padrão"] = "Default microphone",
        ["Atalhos para a saída padrão e o volume geral"] = "Shortcuts for the default output and the master volume",
        ["Trocar também o dispositivo de comunicação"] = "Also switch the communications device",
        ["O dispositivo de comunicação é o que chamadas de voz e vídeo usam, e pode ser diferente do padrão."] = "The communications device is the one voice and video calls use, and it can differ from the default.",
        ["Não foi possível trocar o dispositivo padrão"] = "Could not change the default device",
        ["dispositivo"] = "device",
        ["Nenhuma saída marcada para o atalho"] = "No output is marked for the shortcut",
        ["Só há uma saída marcada para o atalho"] = "Only one output is marked for the shortcut",
        ["Não foi possível ajustar o volume"] = "Could not adjust the volume",
        ["Não foi possível alterar o mudo"] = "Could not change the mute",
        ["slot do Windows"] = "Windows slot",
        ["Aumentar volume geral"] = "Master volume up",
        ["Diminuir volume geral"] = "Master volume down",
        ["Mudo geral"] = "Master mute",
        ["Alternar saída padrão"] = "Switch default output",

        // updates
        ["SlotMixer atualizado"] = "SlotMixer updated",
        ["Agora na versão {0}"] = "Now on version {0}",
        ["Você já está na versão mais recente ({0})"] = "You already have the latest version ({0})",
        ["Baixando a versão {0}…"] = "Downloading version {0}…",
        ["Nova versão do SlotMixer"] = "New SlotMixer version",
        ["A versão {0} está disponível: botão direito no headset"] = "Version {0} is available: right-click the headset",
        ["Não foi possível verificar as atualizações. Confira a conexão e tente de novo."] = "Could not check for updates. Check your connection and try again.",
        ["Atualizando o SlotMixer"] = "Updating SlotMixer",
        ["Para a versão {0}. O widget volta em instantes."] = "To version {0}. The widget will be back in a moment.",
        ["Não foi possível instalar a atualização."] = "Could not install the update.",
        ["Atualizar agora para a versão {0}"] = "Update now to version {0}",
        ["Baixar a versão {0}…"] = "Download version {0}…",
        ["Verificar atualizações agora"] = "Check for updates now",
        ["Atualizar automaticamente"] = "Update automatically",

        ["Ajuda e preferências…"] = "Help and preferences…",
        ["Ajuda e preferências"] = "Help and preferences",
        ["Versão {0}{1}"] = "Version {0}{1}",
        ["Atualizações"] = "Updates",
        ["Suporte"] = "Support",

        // displays
        ["Mostrar o widget: {0}"] = "Show the widget: {0}",
        ["em todos os monitores"] = "on every display",
        ["só no monitor principal"] = "on the main display only",

        // support
        ["Copiar diagnóstico"] = "Copy diagnostics",
        ["Reportar um problema…"] = "Report a problem…",
        ["Não foi possível copiar para a área de transferência."] = "Could not copy to the clipboard.",
        ["Diagnóstico copiado"] = "Diagnostics copied",
        ["Cole na sua mensagem ou na issue do GitHub (Ctrl+V)."] = "Paste it into your message or the GitHub issue (Ctrl+V).",
        ["Reportar um problema"] = "Report a problem",
        ["O diagnóstico foi copiado: cole no campo \"Diagnostics\"."] = "The diagnostics were copied: paste them into the \"Diagnostics\" field.",
        ["Abra a página e descreva o problema."] = "Open the page and describe the problem.",
    };
}
