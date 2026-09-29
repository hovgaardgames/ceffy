# Contributing to Ceffy

Thanks for contributing. Keep changes focused, describe their effect, and include the checks you ran in your pull request. See the [README](README.md) for package setup and local development.

## Code style

The repository's [`.editorconfig`](.editorconfig) defines C# formatting and advisory naming preferences. The C# lint script enforces formatting in `Runtime/`, `Editor/`, and `Samples~/`; its line-length and LINQ findings are warnings. Follow the surrounding code where a convention is not automated.

Keep existing public API names stable. A style-only change should not rename a public member, including names such as `ExecuteJS`. Propose intentional API changes separately and document their compatibility impact.

Put fields at the top of each class, including nested classes. Group them in this order:

1. Public constants
2. Private constants
3. Public static fields
4. Private static fields
5. Public instance fields
6. Private serialized fields
7. Public fields marked `[HideInInspector]`
8. Other private instance fields

Keep attributes and documentation with their fields. Preserve initializer behavior when moving declarations; if a reorder would change behavior, leave it in place and explain the exception in the pull request. Naming suggestions in the IDE are guidance, not a reason to rename existing public API.

## Checks

Run the relevant checks before opening a pull request. The C# formatter requires the .NET 10 SDK; `-Fix` changes formatting only.

```powershell
powershell -ExecutionPolicy Bypass -File .\lint-csharp.ps1
powershell -ExecutionPolicy Bypass -File .\lint-csharp.ps1 -Fix
```

For native C++ changes, run `native~/lint.ps1`. For Unity changes, run relevant EditMode tests in the Test Runner; the Windows native runtime smoke test runs in PlayMode. The [README](README.md#testing) describes how to make the package tests visible in a consuming project.

## Pull requests and releases

Use a [Conventional Commit](https://www.conventionalcommits.org/en/v1.0.0/) title for the pull request, such as `fix: correct shared host focus`. Use `!` or a `BREAKING CHANGE:` footer for an intentional breaking change. Release Please derives version bumps and changelog entries from squash-merged pull request titles on `main`, so routine changes do not need manual changelog entries.
