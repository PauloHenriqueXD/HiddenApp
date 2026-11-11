<p align="center">
  <img src="Assets/logo.png" width="128" alt="HiddenApp">
</p>

<h1 align="center">HiddenApp</h1>

<p align="center">Corte a internet de qualquer janela do Windows com um clique — e automatize cliques com macros.</p>

---

## O que ele faz

- **Lista as janelas abertas** (as mesmas do Alt+Tab), com o ícone de cada programa e uma **prévia ao vivo** da janela selecionada.
- **Bloquear / liberar internet** de um programa com um clique (ou duplo clique na lista). As conexões que já estavam abertas também são derrubadas.
- **Selecionar manualmente:** clique no botão e, em até 3 segundos, na janela que deseja bloquear.
- **Favoritos:** clique na estrela ao lado de uma janela para guardar o programa na lista de favoritos, e bloqueie, libere ou abra com um clique (também pelo ícone da bandeja).
- **Balões de ajuda:** passe o mouse sobre um botão para ver o que ele faz.
- **Cópia bloqueável:** abre outra instância de um programa a partir de uma cópia feita de *hard links* (não ocupa espaço). Só a cópia fica sem internet; o programa original continua normal — útil para, por exemplo, ter duas janelas do mesmo navegador e bloquear só uma. Um programa pode ter **várias cópias**, cada uma com nome, perfil e bloqueio próprios.
- **Bandeja do sistema:** fica rodando perto do relógio; o menu do ícone permite liberar programas, trocar o tema e sair.
- **Iniciar com o Windows** (abre direto na bandeja, sem prompt do UAC).
- **Tema escuro** (padrão) e **tema claro**.
- **Atualização automática:** avisa quando há versão nova no GitHub e atualiza com um clique (sempre pergunta antes). Opção de receber versões beta.

## Apoie o projeto ❤

O HiddenApp é gratuito. Se ele te ajuda, você pode apoiar com um Pix de qualquer valor — pelo botão
**"❤ Apoiar"** dentro do programa ou pela chave aleatória:

```
e4631919-5794-44d5-b5ed-5ebe8c9b5ac8
```

Antes de confirmar, confira se o recebedor é **Paulo Henrique Ferrari Mantuaneli**. Se o nome for outro,
você pode estar usando uma cópia falsa do programa: o HiddenApp oficial só é distribuído por este repositório.

## Auto clique

Na aba **Auto clique** você cria **macros** que clicam sozinhas numa janela:

1. Clique em **Nova** e escolha a **janela alvo**.
2. Adicione os **pontos**: clique em **+ Adicionar ponto** e depois na prévia da janela — ou posicione o mouse
   na própria janela e aperte **F9**.
3. Para cada ponto, escolha o botão, a ação (clique, duplo ou segurar), quantos cliques seguidos, o intervalo
   e a espera antes do próximo ponto.
4. Aperte **F7** para iniciar e **F8** para parar — em qualquer programa. As teclas podem ser trocadas.

Os pontos rodam **em loop**, na ordem. Há dois modos:

| Modo | Como funciona | Observação |
|---|---|---|
| **Segundo plano** | Envia o clique direto para a janela, sem mexer no mouse | Você continua usando o PC; nem todo programa aceita (muitos jogos ignoram) |
| **Clique real** | Move o mouse e clica de verdade | Funciona em quase tudo; a janela precisa estar na frente (pausa sozinho se não estiver) |

> ⚠️ Muitos **jogos online** proíbem macros e auto clique nos termos de uso, e sistemas anti-cheat podem
> **banir a conta**. Confira as regras antes de usar em jogos.

## Como funciona

O bloqueio é feito com regras do **Firewall do Windows** (entrada e saída) para o executável escolhido. As regras ficam salvas no Windows, então o programa continua bloqueado mesmo com o HiddenApp fechado ou após reiniciar o PC — até ser liberado.

Como o Windows separa a internet por **processo/executável** (e não por janela), bloquear uma janela bloqueia o programa inteiro. Para bloquear só uma janela de um programa que tem várias, use a **cópia bloqueável**.

## Requisitos

- Windows 10 ou 11
- Para **usar**: nada além do `HiddenApp.exe` gerado pelo perfil Portatil (o .NET já vem embutido)
- Para **compilar**: [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Executar como **administrador** (o app pede automaticamente — é necessário para criar regras no firewall)
- Firewall do Windows ativo (se um antivírus substituir o firewall do Windows, as regras não têm efeito)

## Compilar

```bash
dotnet build -c Release
```

Ou abra `HiddenApp.sln` no Visual Studio 2022. A interface é montada por código — use **Exibir Código (F7)** em vez do Designer.

### Gerar o executável para compartilhar

Dê dois cliques em **`publicar.bat`** (ou, no Visual Studio: botão direito no projeto → **Publicar** → perfil **Portatil**).

Isso gera **um único `publish\HiddenApp.exe`** que já inclui o .NET — é só copiar esse arquivo para outra máquina com Windows 10/11 64 bits e abrir, sem instalar nada.

Pela linha de comando:

```bash
dotnet publish -p:PublishProfile=Portatil
```

> Ative **"Iniciar com o Windows"** pelo executável final (da pasta `publish`), pois a tarefa de inicialização aponta para o `.exe` que está rodando.

## Lançar uma versão nova

A publicação é automática pelo GitHub Actions ([.github/workflows/release.yml](.github/workflows/release.yml)):

1. Atualize o `<Version>` no `HiddenApp.csproj` e escreva as novidades no `CHANGELOG.md`
   numa seção `## [1.2.0] - AAAA-MM-DD`.
2. Faça commit e envie para o GitHub.
3. Publique de uma das formas:
   - **Pelo site (mais fácil):** no GitHub, aba **Actions** → **Publicar versão** → **Run workflow**,
     digite a versão (ex.: `1.2.0`) e confirme.
   - **Por tag:** `git tag v1.2.0` e `git push origin v1.2.0`.

   Para uma **beta**, use um sufixo: `1.2.0-beta.1`.
4. Em alguns minutos a Release aparece em **Releases**, com o `HiddenApp.exe` e o `HiddenApp.exe.sha256`.
   Os HiddenApp instalados encontram a versão nova sozinhos (estável para todos; beta só para quem ativou "Receber versões beta").

Acompanhe a publicação na aba **Actions** do repositório.

## Onde ficam os dados

| O quê | Onde |
|---|---|
| Programas bloqueados e configurações | `%AppData%\HiddenApp\` |
| Regras de firewall | Firewall do Windows, regras que começam com `HiddenApp - ` |
| Cópias bloqueáveis | `X:\HiddenAppClones\` (no mesmo disco do programa original) |
| Favoritos | `%AppData%\HiddenApp\settings.json` |
| Macros de auto clique | `%AppData%\HiddenApp\macros.json` |
| Inicialização automática | Agendador de Tarefas, tarefa `HiddenApp` |

## Limitações

- Programas que só permitem uma instância podem repassar a janela da cópia para o programa original; para navegadores conhecidos o HiddenApp já preenche um parâmetro de perfil separado.
- Apps da Microsoft Store, programas que acessam a internet por um serviço do Windows e jogos com anti-cheat podem não funcionar com a cópia bloqueável.
- Os arquivos da cópia são *hard links*: se a cópia alterar um arquivo da própria pasta, o original também é alterado.

## Documentação

- [ARQUITETURA.md](ARQUITETURA.md) — como o HiddenApp funciona por dentro
- [CHANGELOG.md](CHANGELOG.md) — histórico de versões
- [CLAUDE.md](CLAUDE.md) — contexto e convenções para quem (ou qual IA) for mexer no código
