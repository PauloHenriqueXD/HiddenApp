# CLAUDE.md — contexto do projeto HiddenApp

Guia para assistentes de IA (Claude, Copilot etc.) e para quem chega agora no código.
Leia antes de mudar qualquer coisa. Detalhes de funcionamento estão em [ARQUITETURA.md](ARQUITETURA.md).

## O que é

App **Windows Forms (.NET 9)** com duas abas: **Internet** (corta a internet de programas escolhidos
usando regras do **Firewall do Windows**) e **Auto clique** (macros que clicam em pontos de uma janela em loop). Inclui prévia ao vivo da janela, "cópia bloqueável"
(segunda instância de um programa feita de hard links), ícone na bandeja, iniciar com o Windows
e temas escuro/claro. Código e textos-base em **português (Brasil)**; interface traduzida para inglês e chinês simplificado.

## Comandos

```bash
# Compilar (Windows)
dotnet build -c Release

# Gerar o .exe único e autocontido para distribuir (saída em publish\)
dotnet publish -p:PublishProfile=Portatil      # ou: publicar.bat

# Compilar fora do Windows (Linux/macOS) — só para checar erros
dotnet build -c Release -p:EnableWindowsTargeting=true
```

Não há testes automatizados. Validação é manual (ver "Checklist de teste" abaixo).

## Estrutura

| Arquivo | Responsabilidade |
|---|---|
| `Program.cs` | Entrada; instância única (Mutex + EventWaitHandle); argumentos `--tray`, `--updated` e `--restart` (troca de idioma) |
| (abas) | `MainForm` tem a barra de abas (`TabButton`) e alterna entre `_body` (Internet) e `_autoClick` |
| `MainForm.cs` | Janela principal: montagem da UI, listas owner-draw, bandeja, ações |
| `CloneDialog.cs` | Diálogo "Abrir cópia bloqueável" (nova cópia ou existente, nome, argumentos) |
| `FirewallManager.cs` | Cria/remove regras via `netsh`; guarda a lista de bloqueados |
| `CloneManager.cs` | Cópias numeradas com hard links em `X:\HiddenAppClones\`, metadados (nome/args), abertura e limpeza |
| `WindowEnumerator.cs` | Lista janelas (estilo Alt+Tab), resolve processo/caminho, PIDs por .exe |
| `WindowPreview.cs` | Miniatura ao vivo via DWM Thumbnails |
| `StartupManager.cs` | "Iniciar com o Windows" via Tarefa Agendada (`schtasks /XML`) |
| `AppSettings.cs` | Preferências em `%AppData%\HiddenApp\settings.json` (tema, favoritos, betas, última versão perguntada) |
| `UpdateManager.cs` | `SemVer`; consulta ao GitHub Releases, download, verificação SHA-256 e troca do .exe |
| `UpdateDialog.cs` | Diálogo "Nova versão disponível" com novidades e progresso do download |
| `AutoClickPage.cs` | Aba "Auto clique": lista de macros, configuração (alvo, modo, loop, pontos), prévia clicável |
| `Macro.cs` | Modelos `Macro`/`MacroStep` (posições relativas 0–1) e `MacroStore` (`macros.json`) |
| `ClickRunner.cs` | Executa macros numa thread: modo segundo plano (`PostMessage`) ou real (`SendInput`), loop, segurar, pausas de segurança; acha a janela alvo e captura a posição do mouse |
| `HotkeyDialog.cs` | `HotkeyBox` (grava uma tecla) e diálogo para trocar as teclas de iniciar/parar/capturar |
| `Donation.cs` | Chave Pix de doação; monta o BR Code ("Pix Copia e Cola", com CRC16) e gera o QR (QRCoder); link do GitHub Sponsors |
| `Feedback.cs` | Botão "Sugestões": abre o Google Forms (`GoogleFormTemplate`) ou uma issue do GitHub já preenchidos com versão/Windows/idioma |
| `.github/ISSUE_TEMPLATE/` | Formulários de issue `sugestao.yml` e `problema.yml` (ids `versao`, `windows`, `idioma` são preenchidos pela URL) |
| `LICENSE`, `PRIVACY.md`, `CODE_SIGNING_POLICY.md` | Licença MIT e políticas exigidas pela SignPath Foundation (assinatura grátis). Mantenha-as verdadeiras: qualquer acesso novo à rede ou mudança no sistema precisa aparecer na política de privacidade e no "Desinstalar" |
| `.github/FUNDING.yml` | Faz o GitHub mostrar o botão "Sponsor" no repositório |
| `SupportDialog.cs` | Janela "Apoiar o projeto": QR, copia e cola, conferência do recebedor e site oficial |
| `I18n.cs` | Traduções: `L.T`/`L.F` (dicionário por texto em português, com modelos `{0}`), `L.Apply(controle)`, `Msg.Show` |
| `Lang/` | `en.json` e `zh-CN.json`: texto em português → tradução (embutidos como `lang.<código>.json`) |
| `Theme.cs` | Paletas, tema atual, escala de DPI (`Theme.S`), barra de título escura, recursos embutidos |
| `ThemedControls.cs` | Controles próprios: `RoundButton` (Primary/Secondary/Danger/Success), `Card`, `ToggleSwitch`, `ThemedListView`, `ThinProgress`, `ThemedToolTip`, renderer do menu |
| `Native.cs` | Todo o P/Invoke (user32, kernel32, dwmapi, iphlpapi, advapi32) |
| `Assets/` | `HiddenApp.ico` (ícone do exe/bandeja) e `logo.png` (cabeçalho), embutidos como recurso |
| `Properties/PublishProfiles/Portatil.pubxml` | Publicação single-file self-contained win-x64 |
| `.github/workflows/release.yml` | Ao enviar tag `v*`: compila no Windows, gera `.exe` + `.sha256` e cria a Release |

## Regras e convenções

- **A UI é montada por código.** Não use o Designer do Visual Studio no `MainForm`/`CloneDialog`
  (ele cria `.resx`/`.Designer.cs` e pode reescrever o arquivo). `MainForm.resx` está no `.gitignore`.
- **Cores sempre de `Theme.Current`** (nunca cores fixas). Controles novos devem ser repintados em
  `MainForm.ApplyTheme()` ou ler a paleta no `OnPaint`. O tema muda em tempo real (`Theme.Changed`).
- **Tamanhos em pixels sempre via `Theme.S(px)`** (escala 125%/150%). Fontes em pontos já escalam sozinhas.
  O app é `SystemAware` de DPI — não trocar para PerMonitor sem revisar `Theme.Dpi`, que é calculado uma vez.
- **Propriedades públicas em controles customizados** precisam de
  `[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]` — no .NET 9 a ausência
  vira **erro** de build (WFO1000).
- **Saída de `netsh`/`schtasks` é localizada** (vem em português no Windows do usuário). Decida
  apenas pelo **código de saída**, nunca pelo texto.
- **Textos da interface, mensagens e comentários em português.** Nomes de código em inglês.
- **Idiomas (pt-BR, en, zh-CN):** o código continua com o texto em português; a tradução é feita na exibição.
  `L.Apply(form)` no fim do construtor traduz os controles (e continua traduzindo quando o `Text` muda);
  textos desenhados à mão, balões, menus, `MessageBox` (use `Msg.Show`) e notificações passam por `L.T`.
  Textos montados com partes variáveis usam **modelos**: `L.F("Internet bloqueada para {0}.", nome)` — não monte
  frases com `$"..."` + condicionais dentro (o modelo não casa). **Todo texto novo entra em `Lang/en.json` e
  `Lang/zh-CN.json`** com a mesma chave em português (e os mesmos `{0}`). Sem tradução, aparece em português.
  O idioma é escolhido no início (`L.Init`); trocar exige reiniciar (`--restart`).
- **Requer administrador** (`app.manifest` → `requireAdministrator`). Tudo que o app abre por conta
  própria (cópias) deve ser iniciado **sem elevação** via `Native.StartUnelevated`.
- Nullable está ativado; mantenha o build **sem warnings**.
- **Todo botão novo ganha um balão de ajuda** via `_tips.Set(botão, título, explicação)` (`ThemedToolTip`),
  explicando o que ele faz na prática. Verde (`ButtonKind.Success`) = liberar; vermelho (`Danger`) = bloquear.
- A lista de janelas tem 5 colunas: **0 = estrela de favorito**, 1 = janela (ocupa o espaço que sobra,
  ver `FitColumns`), 2 = programa, 3 = PID, 4 = internet. Clique na coluna 0 alterna o favorito
  (`IsStarArea`) e não conta como duplo clique de bloqueio.
- Cartões de destaque usam `Card.Accent`: `Favorite` (borda amarela, `Palette.Favorite`) e `Danger` (vermelha).
- Para exibir programas use `DisplayName(path)` (nome amigável + nome da cópia) e `DrawProgramItem`
  para listas (bloqueados/favoritos), em vez de desenhar à mão.

## Armadilhas conhecidas (não "consertar" sem entender)

- **O firewall bloqueia por caminho do .exe, nunca por PID ou janela.** Duas janelas do mesmo
  programa compartilham o bloqueio. A "cópia bloqueável" existe exatamente para contornar isso.
- **Cópias são hard links:** mesmo conteúdo do original. Nunca escreva em arquivos dentro de
  `HiddenAppClones` achando que é uma cópia independente. Hard links só funcionam no mesmo volume,
  por isso a pasta de cópias fica na raiz do disco do programa original.
- **Um programa pode ter várias cópias** (`Nome_HASH` = cópia 1, `Nome_HASH_2`, `Nome_HASH_3`...). Cada uma
  tem pasta, bloqueio e perfil (`_HiddenAppData`) próprios. Nome e argumentos ficam em `_HiddenApp.json`
  dentro da pasta da cópia; "Abrir" (favoritos) reutiliza esses argumentos — sem eles, navegadores
  repassariam a janela para o original.
- **Bloquear uma cópia bloqueia todos os `.exe` da pasta da cópia** (`FirewallManager.TargetsFor`),
  porque muitos apps usam launcher + outro executável. Ao apagar cópias, as regras precisam ser
  removidas **antes** de apagar a pasta (depois não dá para listar os .exe).
- **Iniciar com o Windows usa Tarefa Agendada**, não a chave `Run` do registro: o Windows ignora
  programas `requireAdministrator` na chave `Run`. A tarefa usa `RunLevel=HighestAvailable` (sem UAC)
  e aponta para o caminho do .exe atual (`RefreshIfEnabled` corrige se ele mudar).
- **Iniciar escondido na bandeja** é feito sobrescrevendo `SetVisibleCore` (campo `_allowShow`).
  Por isso a inicialização fica no construtor do `MainForm`, não no evento `Load` (que só dispara
  quando a janela aparece pela primeira vez).
- **O "X" esconde, não fecha.** Sair de verdade é pelo menu da bandeja (`ExitApp`).
- **Prévia DWM** é desenhada em coordenadas do *Form*, não do painel; por isso `WindowPreview`
  converte o retângulo e atualiza num timer.
- **Derrubar conexões abertas** (`KillTcpConnections`) só funciona para TCP IPv4. Conexões IPv6/UDP
  já abertas podem durar até o programa reconectar; as novas são bloqueadas pela regra.
- **Atualização troca o próprio .exe:** o executável em uso é *renomeado* para `.old` (o Windows não
  deixa sobrescrever) e o novo é copiado no mesmo caminho. A versão nova é aberta com `--updated` e
  **espera o Mutex** da antiga (até 20 s) em vez de sair como "segunda instância". O `.old` é apagado
  depois. O caminho não muda, então a tarefa de inicialização e as regras de firewall continuam válidas.
- **Só o executável publicado se atualiza** (`UpdateManager.CanSelfUpdate`): rodando pelo Visual Studio
  existe um `HiddenApp.dll` ao lado do `.exe`, e o app só oferece abrir a página do GitHub.
- **Uma Release sem `HiddenApp.exe.sha256` não é instalada** automaticamente (por segurança).
- **A versão do app vem do `AssemblyInformationalVersion`** (o `<Version>` do csproj, ou o
  `-p:Version` passado pelo workflow a partir da tag). Sufixo `-algo` = beta.
- **Links abertos pelo app** usam `UpdateManager.OpenUrl` (navegador sem privilégio de administrador).
- **Pix de doação:** o código é montado em `Donation.cs` e **nunca** deve vir da internet (um site
  comprometido trocaria a chave). Usar só chave **aleatória** (não CPF/telefone/e-mail). Regras do BR Code:
  nome até 25 caracteres e cidade até 15, sem acentos; o QR é sempre preto no branco. Ao mudar a chave,
  confira o código num app de banco antes de publicar.
- **Auto clique — coordenadas:** pontos são guardados **relativos** à área interna da janela (0–1).
  A thread do clique usa `Native.DpiScope(janela)` para trabalhar na mesma escala de DPI da janela alvo;
  sem isso, cliques erram de lugar em telas com zoom ou em programas sem suporte a DPI.
- **Auto clique — modos:** "segundo plano" usa `PostMessage` no controle filho embaixo do ponto
  (`ChildWindowFromPointEx`); jogos com DirectInput/raw input ignoram (o cursor do jogo até se move, mas o clique
  não vale). "Real" usa `SetCursorPos` + `SendInput` e **só clica com a janela alvo em primeiro plano** (senão pausa)
  — não remova essa proteção.
- **Modos testados e removidos (não funcionaram no WYD):** "segundo plano alternativo" (`SendMessage` + `WM_ACTIVATE`)
  e "real com retorno" (foco temporário). Os valores continuam no enum `ClickMode` só para macros antigas abrirem;
  `MacroStore.Normalize` converte para `SegundoPlano`/`Real`. Não reintroduza sem testar no jogo.
- **Ordem dos modos na interface** é `AutoClickPage.ModeOrder` (índice da lista ≠ valor do enum); ao adicionar um
  modo, inclua nos dois lugares e no `MacroStore.Sanitize`/`Normalize`.
- **Botões só com ícone:** `RoundButton.IconButton(Theme.Glyph.X, "nome", kind)` usa a fonte de ícones do Windows
  (Segoe Fluent Icons / MDL2). O nome vai no balão de ajuda. Cores: verde = criar, vermelho = excluir, ciano = duplicar.
- **Arquivo `.hiddenmacro`:** JSON `{ Formato: "HiddenApp.Macro", Versao: 1, Macros: [...] }`. Ao mudar o formato,
  suba `Versao` e trate versões antigas em `MacroStore.Import` (versões mais novas são recusadas com aviso).
  Ao "segurar", o botão é sempre solto no `finally`, mesmo se a macro for parada.
- **Teclas de atalho globais** usam `RegisterHotKey` no handle do `MainForm` (ids 1–3, tratadas no `WndProc`).
  Se outra aplicação já usa a tecla, o registro falha e o usuário é avisado.
- **Sugestões nunca são enviadas pelo próprio app:** só abrimos um formulário no navegador. Não coloque webhook,
  token ou endereço de envio no `.exe` (qualquer um extrai e usa para spam). O link do Google Forms é um modelo
  com as palavras `VERSAO`, `WINDOWS` e `IDIOMA`; se o formulário mudar, gere um novo link pré-preenchido.
  Os ids dos campos nos modelos de issue precisam bater com os parâmetros da URL em `Feedback.OpenGitHub`.
- **Desinstalar** (`MainForm.Uninstall`): `UnblockAll` **antes** de `DeleteAllClones`, depois `FirewallManager.RemoveAllRules`
  (PowerShell, apaga sobras `HiddenApp - *`) e a tarefa de inicialização. Os dados em `%AppData%\HiddenApp` só são
  apagados no fim do `Program.Main` (`WipeDataOnExit`), quando nada mais salva. Algo novo que o app crie no sistema
  precisa ser desfeito aqui.
- **Visual Studio com arquivos abertos** pode salvar a versão antiga por cima de edições feitas fora
  dele. Ao editar arquivos externamente, recarregue no VS ("Sim para Todos").

## Dados persistidos

| O quê | Onde |
|---|---|
| Programas bloqueados | `%AppData%\HiddenApp\blocked.json` (conferido contra o firewall ao abrir) |
| Preferências (tema, aviso da bandeja) | `%AppData%\HiddenApp\settings.json` |
| Regras de firewall | Nome `HiddenApp - <caminho do exe>`, uma de entrada e uma de saída |
| Cópias | `X:\HiddenAppClones\<Programa>_<hash8>[_N]\` (+ `_HiddenApp.json` com nome e argumentos) |
| Favoritos | `%AppData%\HiddenApp\settings.json` (`Favorites`) |
| Macros | `%AppData%\HiddenApp\macros.json` |
| Teclas de atalho, aba e última macro | `%AppData%\HiddenApp\settings.json` (`HotkeyStart/Stop/Capture`, `LastTab`, `LastMacroId`) |
| Idioma | `%AppData%\HiddenApp\settings.json` (`Language`: `auto`, `pt-BR`, `en`, `zh-CN`) |
| Inicialização | Tarefa agendada `HiddenApp` (argumento `--tray`) |

## Checklist de teste manual

1. Abrir o app (UAC aparece) → lista de janelas com ícones; prévia ao selecionar.
2. Bloquear internet de um navegador → página não carrega; status "BLOQUEADA"; regra aparece em
   "Firewall do Windows com Segurança Avançada". Liberar → volta a funcionar.
3. Abrir cópia bloqueável do Chrome → nova janela sem internet, a original continua com internet.
   Abrir uma segunda cópia (nome "Pessoal", sem bloquear) → as duas cópias têm bloqueio independente.
4. Apagar cópias com a cópia fechada → pasta `HiddenAppClones` some e regras são removidas.
5. Fechar no "X" → app continua na bandeja; menu da bandeja funciona; "Sair" encerra.
6. Alternar tema escuro/claro → toda a janela, barra de título e menu da bandeja mudam.
7. Ativar "Iniciar com o Windows" → reiniciar/fazer logon → app abre só na bandeja, sem UAC.
8. Abrir o app de novo com ele na bandeja → a janela existente aparece (sem segunda instância).
9. Testar em zoom de tela 125%/150%.
10. Atualização: com um `.exe` publicado de versão menor, "Atualizações → Verificar agora" mostra a
    versão nova; "Depois" mantém o botão "Nova versão" no topo e o item na bandeja; "Atualizar agora"
    baixa, reinicia na versão nova e mantém bloqueios/configurações.
11. Favoritos: estrela da lista de janelas (amarela = favorito, cinza = não; clique alterna) e botão direito; bloquear/liberar/abrir pela lista
    e pelo menu da bandeja; abrir uma cópia favorita reabre com o mesmo perfil.
12. Passar o mouse nos botões → balão de ajuda com as cores do tema.
13. "❤ Apoiar" → QR legível pelo app do banco nos dois temas; "Copiar código Pix" funciona; o banco mostra
    o recebedor "Paulo Henrique Ferrari Mantuaneli".
14. Auto clique: criar macro, escolher janela, adicionar pontos pela prévia e com F9; "Testar ponto"; iniciar com F7
    com outra janela em foco e parar com F8; modo real pausa ao trocar de janela; trocar as teclas e conferir que
    as novas funcionam; macros continuam salvas após reabrir o app.
15. Idioma: botão 🌐 → English → reiniciar → janela, balões, menus da bandeja, diálogos e mensagens em inglês;
    repetir com 简体中文 (sem quadradinhos no lugar dos caracteres) e voltar para Português.

## Versão, changelog e lançamento

- Versionamento **SemVer**: `MAIOR.MENOR.CORREÇÃO`, betas com sufixo (`1.2.0-beta.1`).
- A versão fica em `<Version>` no `HiddenApp.csproj`. Ao lançar, atualize **juntos** a versão e o
  [CHANGELOG.md](CHANGELOG.md) (seção `## [X.Y.Z] - AAAA-MM-DD` — o workflow usa ela como texto da Release).
- Para publicar: commit → Actions › "Publicar versão" › Run workflow (digitando `X.Y.Z`), ou
  `git tag vX.Y.Z` + `git push origin vX.Y.Z`. O workflow faz o resto (cria a tag no modo botão).
  Nunca anexe executáveis à mão sem o `.sha256` correspondente.
- Teste de atualização: publique uma beta (`vX.Y.Z-beta.1`), ative "Receber versões beta" num
  executável publicado de versão menor e confira o fluxo completo.
