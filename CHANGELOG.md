# Changelog

Todas as mudanças relevantes do HiddenApp ficam registradas aqui.

O formato segue o [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/)
e o projeto usa [Versionamento Semântico](https://semver.org/lang/pt-BR/).

## [Não lançado]

_Nada ainda._

## [2.5.0] - 2026-11-12

### Adicionado

- **Gatilho pelo mouse:** o gatilho da macro aceita o **botão do meio** e os **botões laterais** do mouse (com Ctrl, Alt
  ou Shift, se quiser) — é só clicar com eles na caixa do gatilho ou escolher em "Mouse ▾". O clique fica reservado
  para o gatilho enquanto a macro aguarda.
- **Botão esquerdo ou direito como gatilho**, só no modo "Uma volta por toque": cada clique **dentro da janela alvo**
  roda uma volta, e o clique continua funcionando normalmente (não é engolido). Cliques durante uma volta são ignorados.

## [2.4.0] - 2026-11-12

### Adicionado

- **Gatilho de macro:** escolha uma tecla de gatilho e o que ela faz — **"Rodar até parar"** (inicia; apertar de novo para)
  ou **"Uma volta por toque"** (cada toque roda a sequência uma vez). Ao clicar em Iniciar, a macro fica **aguardando**
  no cartão "Em execução" (bolinha azul, etiqueta GATILHO) e volta a aguardar depois de cada execução, até ser parada.
  Funciona no modo real e em segundo plano, em qualquer programa.
- **Mesma tecla para iniciar e parar:** ela passa a ligar e desligar (se algo estiver rodando, para tudo; senão inicia).

## [2.3.1] - 2026-11-12

### Corrigido

- Voltar o HiddenApp de minimizado às vezes fazia o Windows tocar o aviso sonoro e pedia um segundo clique: a lista de
  janelas agora é atualizada só depois que a janela termina de aparecer (e no máximo a cada 3 segundos).
- Trocar as teclas de atalho: apertar a tecla que já era um atalho (ex.: F7) iniciava a macro em vez de ser gravada.
  Enquanto o diálogo de teclas ou a caixa "Tecla" de um passo estão em uso, os atalhos ficam desligados.
- Passo de tecla não aceita mais as teclas de atalho do HiddenApp (a macro se iniciaria/pararia sozinha ao rodar).

### Alterado

- Todos os campos da macro têm explicação ao passar o mouse, tanto no nome do campo quanto na caixa (inclusive nas
  setinhas dos campos numéricos).

## [2.3.0] - 2026-11-11

### Adicionado

- **Ajuste fino da posição do clique:** campos "Posição (%)" (horizontal × vertical) no passo selecionado, além do botão
  de reposicionar pela prévia.
- **Prévia ampliada:** botão "Ampliar prévia" abre a janela alvo numa janela grande (pode maximizar), onde também dá
  para adicionar e reposicionar pontos com mais precisão.
- Favoritos (aba Internet): botão para **abrir cópia bloqueável** do favorito selecionado.

### Alterado

- Favoritar macro igual à lista de janelas: **clique na estrela** dentro da caixa de macros (o botão de estrela saiu).
- Macro favorita **não pode ser excluída**: é preciso tirar a estrela antes.

## [2.2.0] - 2026-11-11

### Adicionado

- A aba **"Auto clique" virou "Macro"**: além de cliques, os passos agora podem ser **teclas** (com Ctrl, Alt ou Shift),
  com as ações "Apertar" ou "Segurar", repetições, intervalo e espera. Botão de teclado verde adiciona um passo de
  tecla depois do passo selecionado. No modo real, as teclas são enviadas como teclado físico (funciona em jogos);
  no segundo plano, vão direto para a janela.
- **Agendar macro:** "Iniciar às" um horário, **todo dia** ou **só uma vez**. O HiddenApp precisa estar aberto (pode
  estar escondido perto do relógio) e a janela alvo, aberta; no modo real a janela é trazida para a frente.
- **Macros favoritas:** botão de estrela (ou botão direito na caixa de macros); as favoritas aparecem com ★ no topo
  da lista e no menu da bandeja.
- **Várias macros ao mesmo tempo:** o cartão **"Em execução"** (embaixo da prévia) lista as macros rodando, com o
  progresso de cada uma, e permite parar uma ou todas. Em segundo plano, quantas quiser; no modo **real**, só uma por
  vez (ela usa o mouse e o teclado de verdade). A tecla de parar (F8) para todas; o botão "Parar" para a selecionada.
  Na bandeja, as macros rodando aparecem marcadas (clique para parar só ela).
- **Ordenar macros:** botões para subir e descer a macro na lista (dentro do grupo de favoritas ou das demais).

### Alterado

- Macros novas começam no modo **real** (mouse e teclado de verdade).
- A lista de macros virou uma **caixa de seleção** no topo do cartão "Macro" (com estrela, alvo e agendamento em cada
  item), deixando a coluna da esquerda livre para as configurações — melhor em telas pequenas.
- Aba Internet: botões com **ícones** (o nome e a explicação aparecem no balão ao passar o mouse); "Bloquear internet"
  e "Liberar internet" mostram ícone e texto.
- Arquivos `.hiddenmacro` passam para a versão 2 do formato (versões antigas do HiddenApp pedem para atualizar).

## [2.1.0] - 2026-11-11

### Adicionado

- **Desinstalar (remover tudo do computador)**, em "Atualizações ▾" e no menu da bandeja: remove todas as regras
  de firewall do HiddenApp, as cópias bloqueáveis e a inicialização com o Windows; opcionalmente apaga
  configurações, favoritos e macros. Depois abre a pasta do programa para apagar o `.exe`.
- Licença **MIT**, política de privacidade e política de assinatura de código (preparação para assinatura
  gratuita pela SignPath Foundation).
- Nome do autor, copyright e descrição nas propriedades do `.exe`.

## [2.0.1] - 2026-11-11

### Adicionado

- Botão **"Sugestões"** no topo (e no menu da bandeja): enviar comentário ou sugestão pelo **Google Forms**
  (sem conta) ou pelo **GitHub** (sugerir melhoria / relatar problema). O formulário abre no navegador já com a
  versão do HiddenApp, do Windows e o idioma; nada é enviado sem a pessoa confirmar.
- Modelos de issue no GitHub para sugestões e problemas.

### Corrigido

- Alguns textos que ficavam em português nos outros idiomas ("Sair", "Cancelado.", "Parar", variação aleatória...).

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
  - **Segundo plano:** manda o clique direto para a janela, sem mexer no mouse (nem todo programa aceita;
    muitos jogos ignoram).
  - **Clique real:** move o mouse e clica de verdade; **pausa sozinho** se a janela alvo sair da frente,
    para não clicar em outro programa.
- **Marcar pontos** clicando na **prévia ao vivo** da janela alvo (o modo "+ Adicionar pontos" fica ligado até
  "✓ Concluir", então dá para marcar vários seguidos), ou posicionando o mouse na janela e apertando a tecla de captura. As posições são relativas à janela: continuam certas se ela mudar de lugar ou de tamanho.
- **Teclas de atalho globais** (funcionam em qualquer programa): **F7 inicia**, **F8 para**, **F9 captura o ponto**.
  Podem ser trocadas em "Teclas de atalho", com combinações de Ctrl, Alt e Shift.
- Janela "Apoiar o projeto" com opção para quem está **fora do Brasil**: link para o **GitHub Sponsors**
  (o repositório também passou a mostrar o botão "Sponsor").
- **Nome ou descrição em cada ponto** (ex.: "Botão Coletar"), mostrado na lista de pontos.
- Botões da lista de macros e dos pontos com **ícones coloridos** (verde = criar, vermelho = excluir, ciano = duplicar),
  com o nome e a explicação no balão ao passar o mouse.
- **Exportar e importar macros** (arquivo `.hiddenmacro`) para compartilhar com outras pessoas. Valores fora do normal
  são corrigidos na importação, e a janela alvo é procurada pelo nome do programa se ele estiver em outra pasta.
- Tempos padrão de **100 ms** (intervalo, espera e segurar).
- **Idiomas:** português, **inglês** e **chinês simplificado** (简体中文). Por padrão segue o idioma do Windows;
  o botão 🌐 no topo troca o idioma (o app reinicia para aplicar).
- README em inglês ([README.en.md](README.en.md)).
- Layout em três colunas que cabe na tela sem rolagem: macros e configuração, pontos e ponto selecionado, prévia;
  barra de Iniciar/Parar fixa embaixo, com instruções passo a passo.
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
