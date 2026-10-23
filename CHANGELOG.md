# Changelog

Cada versão ganha uma seção `## X.Y.Z`; o texto dela vira o corpo do Release no GitHub (veja `tools/release-notes.ps1`).

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
