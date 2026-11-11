# Changelog

Todas as mudanças relevantes do HiddenApp ficam registradas aqui.

O formato segue o [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/)
e o projeto usa [Versionamento Semântico](https://semver.org/lang/pt-BR/).

## [Não lançado]

_Nada ainda._

## [2.0.0] - 2026-11-11

Segunda fase do projeto: **auto clique**.

### Adicionado

- **Abas no topo:** "Internet" (tudo o que já existia) e **"Auto clique"**.
- **Macros de auto clique:** cada macro tem uma janela alvo e uma lista de **pontos** que rodam em loop, na ordem.
  - Por ponto: botão (esquerdo, direito ou meio), ação (clique, clique duplo ou **apertar e segurar** por um tempo),
    **quantos cliques seguidos no mesmo lugar**, intervalo entre eles e espera antes do próximo ponto.
  - Repetir a sequência **sem parar** ou um **número de vezes**; variação aleatória opcional nos tempos.
  - Macros salvas automaticamente, com criar, duplicar e excluir.
- **Dois modos de clique:**
  - **Segundo plano:** manda o clique direto para a janela, sem mexer no mouse (nem todo programa aceita).
  - **Clique real:** move o mouse e clica de verdade; **pausa sozinho** se a janela alvo sair da frente,
    para não clicar em outro programa.
- **Marcar pontos** clicando na **prévia ao vivo** da janela alvo, ou posicionando o mouse na janela e apertando
  a tecla de captura. As posições são relativas à janela: continuam certas se ela mudar de lugar ou de tamanho.
- **Teclas de atalho globais** (funcionam em qualquer programa): **F7 inicia**, **F8 para**, **F9 captura o ponto**.
  Podem ser trocadas em "Teclas de atalho", com combinações de Ctrl, Alt e Shift.
- Botão **"Testar ponto"** para conferir se o clique acerta o lugar e se o modo funciona naquele programa.
- Menu da bandeja: iniciar qualquer macro e **parar o auto clique**; a dica do ícone mostra a macro em execução.
- Para automaticamente se a janela alvo for fechada; pausa enquanto ela estiver minimizada.

## [1.3.1] - 2026-11-11

### Alterado

- **Estrela de favorito na lista de janelas:** cada janela tem uma estrela no começo da linha —
  amarela quando o programa é favorito, cinza quando não. Um clique adiciona ou remove dos favoritos.
  O botão "★ Favoritar" foi removido.
- **Favoritos à esquerda e Programas bloqueados à direita**, com bordas coloridas:
  amarela nos favoritos e vermelha nos bloqueados.

### Corrigido

- A linha embaixo do cabeçalho ficava escondida atrás dos botões do lado direito.

## [1.3.0] - 2026-11-11

### Adicionado

- **Apoiar o projeto:** botão "❤ Apoiar" no topo e item no menu da bandeja abrem uma janela com
  QR code do Pix e o código "Pix Copia e Cola" (com botão para copiar), para doações de qualquer valor.
  - O código Pix é gerado dentro do programa (nunca baixado da internet).
  - A janela mostra o nome do recebedor para conferência no app do banco e o endereço do site oficial,
    para proteger contra cópias falsas do programa.

## [1.2.0] - 2026-11-10

### Adicionado

- **Favoritos:** nova lista ao lado dos programas bloqueados para guardar os programas que você mais usa.
  - Bloquear, liberar e abrir com um clique (duplo clique alterna o bloqueio), mesmo com o programa fechado.
  - Adicionar pelo botão "★ Favoritar", pelo botão direito numa janela ou programa bloqueado, ou escolhendo um `.exe`.
  - Submenu "Favoritos" no ícone da bandeja para bloquear, liberar ou abrir sem abrir a janela.
- **Várias cópias bloqueáveis por programa:** cada cópia tem bloqueio, perfil e nome próprios
  (ex.: "Chrome — Trabalho" sem internet e "Chrome — Pessoal" com internet).
  O diálogo permite escolher entre criar uma cópia nova ou reabrir uma existente.
- **Balões de ajuda:** passar o mouse sobre qualquer botão mostra o que ele faz na prática.
- **Menu do botão direito** na lista de janelas e nas listas de bloqueados e favoritos.
- Programas bloqueados e favoritos aparecem com o nome amigável (ex.: "Google Chrome") e a situação
  (SEM INTERNET / COM INTERNET).

### Alterado

- Botão "Cortar internet" renomeado para **"Bloquear internet"**.
- Botões "Liberar internet" e "Liberar selecionado" agora são **verdes**.
- Botão "Escolher janela (3s)" renomeado para **"Selecionar manualmente"**.
- A cópia passou a mostrar o seu nome na lista de janelas no lugar da etiqueta genérica "CÓPIA".

## [1.1.0] - 2026-11-10

### Adicionado

- **Atualização automática pelo GitHub Releases.** O HiddenApp verifica se há versão nova ao abrir
  e a cada 6 horas, mostra as novidades e **sempre pergunta** antes de atualizar.
  - Se o usuário escolher "Depois", a versão nova continua indicada no topo da janela,
    no menu e na dica do ícone da bandeja.
  - Com a janela escondida, o aviso aparece como notificação da bandeja.
  - O arquivo baixado é conferido por **SHA-256** antes de substituir o executável.
  - O app reinicia sozinho na versão nova; bloqueios, configurações e "Iniciar com o Windows" são mantidos.
- **Versões beta (pré-lançamento):** opção "Receber versões beta" no menu "Atualizações".
- Menu **"Atualizações"** no cabeçalho: verificar agora, versões beta, versão atual e link para todas as versões.
- Item **"Verificar atualizações"** no menu da bandeja.
- Número da versão exibido ao lado do nome do app.
- **Publicação automática:** ao enviar uma tag `vX.Y.Z` (ou `vX.Y.Z-beta.N`), o GitHub Actions compila,
  gera o executável e o `.sha256` e cria a Release com as novidades do CHANGELOG.

## [1.0.0] - 2026-11-10

Primeira versão.

### Adicionado

- **Bloqueio de internet por programa** usando regras do Firewall do Windows (entrada e saída),
  com derrubada das conexões TCP que já estavam abertas.
- **Lista de janelas abertas** (estilo Alt+Tab) com ícone do programa, PID e status
  (BLOQUEADA / LIBERADA / SEM ACESSO); duplo clique alterna o bloqueio.
- **Escolher janela (3s):** seleciona a janela que estiver em primeiro plano após 3 segundos.
- **Prévia ao vivo** da janela selecionada (DWM Thumbnails), com aviso para janela minimizada ou fechada.
- **Cópia bloqueável:** abre uma segunda instância de um programa a partir de uma cópia feita de
  hard links em `X:\HiddenAppClones\`, permitindo cortar a internet só dela.
  - Argumentos sugeridos automaticamente para Chrome, Edge, Brave, Vivaldi, Opera e Firefox.
  - Bloqueio cobre todos os `.exe` da pasta da cópia.
  - Cópia aberta sem privilégio de administrador.
  - Aviso quando o programa repassa a janela para a instância original.
  - Botão "Apagar cópias" (não afeta os programas originais).
- **Lista de programas bloqueados**, que continuam bloqueados mesmo com o app fechado ou após reiniciar.
- **Ícone na bandeja do sistema** com menu: abrir, liberar programas, liberar todos, tema,
  iniciar com o Windows e sair. Fechar a janela no "X" mantém o app na bandeja.
- **Iniciar com o Windows** via Tarefa Agendada (abre direto na bandeja, sem prompt do UAC).
- **Tema escuro (padrão) e claro**, incluindo barra de título e barras de rolagem.
- **Instância única:** abrir o app de novo mostra a janela já aberta.
- Ajuste automático de tamanhos para telas com zoom (125%, 150%...).
- Ícone próprio do aplicativo.
- Perfil de publicação **Portatil** e `publicar.bat`: gera um único `HiddenApp.exe` autocontido
  (não precisa instalar o .NET).

[Não lançado]: https://github.com/PauloHenriqueXD/HiddenApp/compare/v2.0.0...HEAD
[2.0.0]: https://github.com/PauloHenriqueXD/HiddenApp/releases/tag/v2.0.0
[1.3.1]: https://github.com/PauloHenriqueXD/HiddenApp/releases/tag/v1.3.1
[1.3.0]: https://github.com/PauloHenriqueXD/HiddenApp/releases/tag/v1.3.0
[1.2.0]: https://github.com/PauloHenriqueXD/HiddenApp/releases/tag/v1.2.0
[1.1.0]: https://github.com/PauloHenriqueXD/HiddenApp/releases/tag/v1.1.0
[1.0.0]: https://github.com/PauloHenriqueXD/HiddenApp/releases/tag/v1.0.0
