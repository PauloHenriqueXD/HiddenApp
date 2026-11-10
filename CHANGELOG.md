# Changelog

Todas as mudanças relevantes do HiddenApp ficam registradas aqui.

O formato segue o [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/)
e o projeto usa [Versionamento Semântico](https://semver.org/lang/pt-BR/).

## [Não lançado]

_Nada ainda._

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

[Não lançado]: https://github.com/PauloHenriqueXD/HiddenApp/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/PauloHenriqueXD/HiddenApp/releases/tag/v1.0.0
