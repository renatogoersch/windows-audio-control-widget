# Assinatura do exe com a SignPath (opcional, desligada)

> **Estado: esqueleto pronto, nunca executado.** O workflow, o `tools\build-zips.ps1 -SignedExeDir` e este roteiro foram escritos, mas nada disso foi testado: falta a conta na SignPath. Enquanto a variável `SIGNPATH_ORGANIZATION_ID` não existir no repositório, o workflow **Release** se comporta exatamente como antes (compila, zipa e publica sem assinar).

## Para que serve

O exe não é assinado, então o Windows SmartScreen avisa na primeira abertura ("O Windows protegeu o seu PC"). A [SignPath Foundation](https://signpath.org/foundation) assina de graça projetos de código aberto. O certificado sai em nome da SignPath Foundation (é isso que aparece como "Editor"), e a reputação no SmartScreen ainda se acumula com o tempo e com os downloads: assinar reduz o aviso, mas não o zera de imediato.

## Como funciona aqui

Com a variável ligada, o `release.yml` faz:

1. `tools\build-zips.ps1 -NoZips -ExeOutDir unsigned`: compila os dois exes e os copia como `SlotMixer-requer-dotnet.exe` e `SlotMixer-autonomo.exe`.
2. `actions/upload-artifact`: sobe esses dois exes.
3. `signpath/github-action-submit-signing-request`: pede a assinatura e baixa os exes assinados em `signed\`.
4. `tools\build-zips.ps1 -SignedExeDir signed`: monta os zips com os exes assinados (confere que cada um é da versão da tag; se faltar algum, o build falha e **nada é publicado**).
5. Notas e `gh release create`, como sempre.

O resto do app não muda: o atualizador confere o tamanho e a soma SHA-256 do **zip** e a versão do exe, e nada disso depende da assinatura.

## Passo a passo (você, uma vez)

1. **Candidate-se** em <https://signpath.org/foundation> (confira as condições atuais lá). O projeto cumpre o básico: licença MIT, repositório público, builds no GitHub Actions. O programa costuma pedir também uma nota de "política de assinatura de código" no README (algo como "Assinatura de código gratuita por SignPath.io, certificado da SignPath Foundation"); inclua o texto que eles indicarem.
2. Depois da aprovação, em <https://app.signpath.io>, na sua organização:
   - crie o **projeto** (anote o *slug*, por exemplo `slotmixer`);
   - crie as **políticas de assinatura**: `test-signing` (certificado de teste, sem aprovação) e `release-signing` (certificado real);
   - em **Trusted build systems**, vincule o **GitHub.com** à organização e permita este repositório (siga o guia da SignPath para GitHub Actions);
   - crie uma **configuração de artefato** (*artifact configuration*) para o zip que o `upload-artifact` gera, com dois exes na raiz:

     ```xml
     <?xml version="1.0" encoding="utf-8"?>
     <artifact-configuration xmlns="http://signpath.io/artifact-configuration/v1">
       <zip-file>
         <pe-file path="SlotMixer-requer-dotnet.exe">
           <authenticode-sign />
         </pe-file>
         <pe-file path="SlotMixer-autonomo.exe">
           <authenticode-sign />
         </pe-file>
       </zip-file>
     </artifact-configuration>
     ```

   - crie um **usuário de CI** e um **token de API** para ele.
3. No GitHub, em *Settings › Secrets and variables › Actions*:
   - **Secret** `SIGNPATH_API_TOKEN`: o token;
   - **Variables**: `SIGNPATH_ORGANIZATION_ID` (o ID da organização, que é o que liga a assinatura), `SIGNPATH_PROJECT_SLUG`, `SIGNPATH_SIGNING_POLICY_SLUG` (comece com `test-signing`) e, se criou uma configuração que não é a padrão, `SIGNPATH_ARTIFACT_CONFIGURATION_SLUG`.
4. **Teste com uma versão de verdade** (a tag dispara o workflow): confira nos logs que a assinatura foi pedida e concluída e, no zip publicado, `Get-AuthenticodeSignature .\SlotMixer.exe` (com `test-signing` o certificado não é confiável, é só para ver o fluxo). Abra o exe assinado e veja o menu e a atualização funcionarem.
5. Troque `SIGNPATH_SIGNING_POLICY_SLUG` para `release-signing`.
6. Para **desligar**, apague a variável `SIGNPATH_ORGANIZATION_ID`.

## Pontos a conferir na primeira execução real

- Se a ação da SignPath entrega os exes **extraídos** em `signed\` (o roteiro assume isso). Se vier um `.zip` dentro da pasta, o `-SignedExeDir` falha de propósito com "Signed exe not found" e o ajuste é só descompactar antes de chamar o script.
- Se o `permissions: actions: read` que o `release.yml` declara é o bastante para a ação (a SignPath lê o artefato pela API do GitHub).
- Se o `.exe` de arquivo único (o do zip autônomo embute o .NET) continua abrindo depois de assinado. A assinatura é feita sobre o exe final, depois do empacotamento, que é o caso suportado.
- Nomes dos parâmetros da ação (`github-artifact-id`, `output-artifact-directory`, ...): estão conforme a documentação da versão `v1`; confira se mudou.