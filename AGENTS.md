# Agent instructions

## Commits and pull requests

This repo uses [release-please](https://github.com/googleapis/release-please), which builds the changelog and version bumps from **squash-merged PR titles** on `main`. Manual `CHANGELOG.md` Unreleased entries are not required for routine work.

Use [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) for:

- Git commit messages
- Pull request titles (the squash commit on `main` is what release-please reads)

### Format

```
<type>[optional scope][optional !]: <description>
```

Common types: `feat`, `fix`, `docs`, `chore`, `refactor`, `test`, `ci`.

### Version impact

| Example | Effect |
|---|---|
| `feat: add shared instance views` | minor bump |
| `fix: correct keyboard focus null check` | patch bump |
| `feat!: rename WebBrowser to CeffyInstance` | major bump (breaking) |
| `feat: rename API` with a `BREAKING CHANGE:` footer | major bump |

Prefer `!` after the type/scope (or a `BREAKING CHANGE:` body/footer) when the change is intentionally breaking.

### Examples

```
feat: add shared-instance mode
fix: use Unity lifetime check for shared host focus
docs: document conventional commits for agents
chore: merge main into feature branch
```

Do not invent changelog sections by hand unless release-please cannot express the change and a maintainer asks for it.
