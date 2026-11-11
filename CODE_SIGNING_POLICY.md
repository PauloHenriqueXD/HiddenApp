# Política de assinatura de código / Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by
[SignPath Foundation](https://signpath.org/).

*(Enquanto a assinatura não estiver ativa, os executáveis são publicados sem assinatura. /
Until signing is active, executables are published unsigned.)*

## Funções / Team roles

| Função / Role | Membros / Members |
|---|---|
| Autores / Committers | [PauloHenriqueXD](https://github.com/PauloHenriqueXD) |
| Revisores / Reviewers | [PauloHenriqueXD](https://github.com/PauloHenriqueXD) |
| Aprovadores / Approvers | [PauloHenriqueXD](https://github.com/PauloHenriqueXD) |

Todos os membros usam autenticação em dois fatores no GitHub. / All members use multi-factor authentication.

## Como os executáveis são gerados / How binaries are built

- Só o `HiddenApp.exe` do próprio projeto é assinado. / Only HiddenApp's own binary is signed.
- O executável é compilado pelo GitHub Actions a partir do código deste repositório
  ([release.yml](.github/workflows/release.yml)); cada versão assinada é aprovada manualmente.
  / Built by GitHub Actions from this repository's source; each signed release is approved manually.
- Cada Release traz também o `HiddenApp.exe.sha256`. / Every release also ships a SHA-256 checksum.

## Privacidade / Privacy

Veja a [política de privacidade](PRIVACY.md). Este programa não envia nenhuma informação para outros
sistemas em rede, exceto a verificação de atualizações no GitHub e as páginas que você mesmo abre.
/ This program will not transfer any information to other networked systems unless specifically requested
by the user, except the GitHub update check described in the privacy policy.
