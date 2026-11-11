# CLAUDE.md — contexto do projeto HiddenApp

Guia para assistentes de IA (Claude, Copilot etc.) e para quem chega agora no código.
Leia antes de mudar qualquer coisa. Detalhes de funcionamento estão em [ARQUITETURA.md](ARQUITETURA.md).

## O que é

App **Windows Forms (.NET 9)** que corta a internet de programas escolhidos pelo usuário,
usando regras do **Firewall do Windows**. Inclui prévia ao vivo da janela, "cópia bloqueável"
(segunda instância de um programa feita de hard links), ícone na bandeja, iniciar com o Windows
e temas escuro/claro. Interface e textos em **português (Brasil)**.

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
| `Program.cs` | Entrada; instância única (Mutex + EventWaitHandle); argumentos `--tray` e `--updated` |
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
- **Requer administrador** (`app.manifest` → `requireAdministrator`). Tudo que o app abre por conta
  própria (cópias) deve ser iniciado **sem elevação** via `Native.StartUnelevated`.
- Nullable está ativado; mantenha o build **sem warnings**.
- **Todo botão novo ganha um balão de ajuda** via `_tips.Set(botão, título, explicação)` (`ThemedToolTip`),
  explicando o que ele faz na prática. Verde (`ButtonKind.Success`) = liberar; vermelho (`Danger`) = bloquear.
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
11. Favoritos: adicionar pelo "★ Favoritar" e pelo botão direito; bloquear/liberar/abrir pela lista
    e pelo menu da bandeja; abrir uma cópia favorita reabre com o mesmo perfil.
12. Passar o mouse nos botões → balão de ajuda com as cores do tema.

## Versão, changelog e lançamento

- Versionamento **SemVer**: `MAIOR.MENOR.CORREÇÃO`, betas com sufixo (`1.2.0-beta.1`).
- A versão fica em `<Version>` no `HiddenApp.csproj`. Ao lançar, atualize **juntos** a versão e o
  [CHANGELOG.md](CHANGELOG.md) (seção `## [X.Y.Z] - AAAA-MM-DD` — o workflow usa ela como texto da Release).
- Para publicar: commit → Actions › "Publicar versão" › Run workflow (digitando `X.Y.Z`), ou
  `git tag vX.Y.Z` + `git push origin vX.Y.Z`. O workflow faz o resto (cria a tag no modo botão).
  Nunca anexe executáveis à mão sem o `.sha256` correspondente.
- Teste de atualização: publique uma beta (`vX.Y.Z-beta.1`), ative "Receber versões beta" num
  executável publicado de versão menor e confira o fluxo completo.
