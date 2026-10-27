# Distribuição, CI e suporte / Distribution, CI and support

- [Português](#português)
- [English](#english)

---

## Português

### Fluxo de release (resumo)

1. Escreva a seção `## X.Y.Z` no [CHANGELOG.md](../CHANGELOG.md).
2. Com tudo commitado em `main`: `tools\release.ps1 -Version X.Y.Z` (grava a versão, cria a tag `vX.Y.Z` e a envia).
3. A tag dispara o workflow **Release**: compila as duas versões, monta `SlotMixer-requer-dotnet.zip` e `SlotMixer-autonomo.zip` e publica o Release. Em até 6 horas as cópias instaladas se atualizam sozinhas.
4. Depois do Release, atualize os pacotes de terceiros (abaixo): `tools\scoop-manifest.ps1 -Version X.Y.Z` e `tools\winget-manifest.ps1 -Version X.Y.Z`.

### CI

O workflow **CI** (`.github/workflows/ci.yml`) compila o projeto a cada push em `main` e em cada pull request. Quando existir `tests/SlotMixer.Tests/SlotMixer.Tests.csproj`, ele também roda `dotnet test`; até lá essa etapa só avisa que não há testes.

### winget

O pacote é `RenatoGoersch.SlotMixer`, instalado a partir do zip **autônomo** (sem dependência do .NET), como aplicativo portátil com o comando `slotmixer`.

1. Gere os manifestos de um Release já publicado:

   ```powershell
   tools\winget-manifest.ps1 -Version X.Y.Z
   ```

   Eles vão para `dist\winget\X.Y.Z\` (versão, instalador e os idiomas en-US e pt-BR) e, se o `winget` estiver instalado, passam por `winget validate`.
2. Envie para o repositório de pacotes, **à mão** (exige conta no GitHub e revisão da Microsoft):
   - com o [wingetcreate](https://github.com/microsoft/winget-create): `wingetcreate submit dist\winget\X.Y.Z` (ou, nas versões seguintes, `wingetcreate update RenatoGoersch.SlotMixer --version X.Y.Z --urls <url do zip autônomo> --submit`);
   - ou faça um fork de `microsoft/winget-pkgs`, copie a pasta para `manifests\r\RenatoGoersch\SlotMixer\X.Y.Z\` e abra o pull request.
3. Depois de aprovado: `winget install RenatoGoersch.SlotMixer`.

Observação: o exe vem de uma pasta do winget, não da pasta de instalação do próprio SlotMixer; na primeira abertura ele oferece instalar. Responder **Não** mantém a cópia gerenciada pelo winget (ele não pergunta de novo); essa cópia só avisa de versões novas, e a atualização vem do `winget upgrade`.

### Scoop

Este repositório também é um *bucket* do Scoop (`bucket\slotmixer.json`):

```powershell
scoop bucket add slotmixer https://github.com/renatogoersch/windows-audio-control-widget
scoop install slotmixer/slotmixer
```

Atualização: `scoop update slotmixer`. Para publicar uma versão nova no bucket, depois do Release: `tools\scoop-manifest.ps1 -Version X.Y.Z` e commit em `main`. O manifesto também tem `checkver` e `autoupdate`, então as ferramentas do próprio Scoop (por exemplo o *Excavator* ou `checkver -u`) conseguem atualizá-lo, mas isso não está automatizado aqui. O Scoop abre o app com `--portable`, que o impede de oferecer a instalação.

### Assinatura do exe

Ainda **desligada**. O esqueleto (SignPath Foundation, grátis para código aberto) está pronto e documentado em [signing.md](signing.md); nunca foi executado, pois depende de uma conta que ainda não existe.

### Suporte

Os formulários de issue (`.github/ISSUE_TEMPLATE`) pedem versão, qual zip, versão do Windows e o texto de **Copiar diagnóstico** (botão direito no ícone do headset no widget). Esse texto reúne versões, monitores e escala, posição da barra e o log de erros, e é o que permite reproduzir problemas de setups de monitores que não temos aqui.

---

## English

### Release flow (recap)

1. Write the `## X.Y.Z` section in [CHANGELOG.md](../CHANGELOG.md).
2. With everything committed on `main`: `tools\release.ps1 -Version X.Y.Z` (sets the version, creates and pushes the tag `vX.Y.Z`).
3. The tag starts the **Release** workflow: it builds both flavors, makes `SlotMixer-requer-dotnet.zip` and `SlotMixer-autonomo.zip`, and publishes the Release. Installed copies update themselves within 6 hours.
4. After the Release, refresh the third-party packages (below): `tools\scoop-manifest.ps1 -Version X.Y.Z` and `tools\winget-manifest.ps1 -Version X.Y.Z`.

### CI

The **CI** workflow (`.github/workflows/ci.yml`) builds the project on every push to `main` and on every pull request. Once `tests/SlotMixer.Tests/SlotMixer.Tests.csproj` exists it also runs `dotnet test`; until then that step just says there are no tests.

### winget

The package is `RenatoGoersch.SlotMixer`, installed from the **self-contained** zip (no .NET dependency) as a portable app with the command `slotmixer`.

1. Generate the manifests from a published Release:

   ```powershell
   tools\winget-manifest.ps1 -Version X.Y.Z
   ```

   They go to `dist\winget\X.Y.Z\` (version, installer, en-US and pt-BR locales) and, when `winget` is installed, are checked with `winget validate`.
2. Submit them **by hand** (needs a GitHub account and Microsoft's review):
   - with [wingetcreate](https://github.com/microsoft/winget-create): `wingetcreate submit dist\winget\X.Y.Z` (for later versions, `wingetcreate update RenatoGoersch.SlotMixer --version X.Y.Z --urls <self-contained zip url> --submit`);
   - or fork `microsoft/winget-pkgs`, copy the folder to `manifests\r\RenatoGoersch\SlotMixer\X.Y.Z\` and open the pull request.
3. Once approved: `winget install RenatoGoersch.SlotMixer`.

Note: the exe runs from a winget folder, not from SlotMixer's own install folder, so the first run offers to install. Answering **No** keeps the copy managed by winget (it does not ask again); that copy only announces new versions, and `winget upgrade` does the updating.

### Scoop

This repository is also a Scoop *bucket* (`bucket\slotmixer.json`):

```powershell
scoop bucket add slotmixer https://github.com/renatogoersch/windows-audio-control-widget
scoop install slotmixer/slotmixer
```

Update with `scoop update slotmixer`. To publish a new version to the bucket after a Release, run `tools\scoop-manifest.ps1 -Version X.Y.Z` and commit to `main`. The manifest also has `checkver` and `autoupdate` blocks, so Scoop's own tooling (for example *Excavator* or `checkver -u`) can bump it, but that is not automated here. Scoop starts the app with `--portable`, which stops it from offering to install.

### Code signing

Still **off**. The scaffold (SignPath Foundation, free for open source) is in place and documented in [signing.md](signing.md); it has never been run because it needs an account that does not exist yet.

### Support

The issue forms (`.github/ISSUE_TEMPLATE`) ask for the version, which zip, the Windows version and the text from **Copy diagnostics** (right-click the headset icon on the widget). That text collects versions, monitors and scaling, the taskbar position and the error log, which is what makes it possible to reproduce problems on monitor setups we do not have here.