# Contributing

## Explain your use case

In your issue or pull request, explain how you use or plan to use this project, what problem you encountered, and how the proposed change helps.

Follow this repository’s setup and validation instructions, and report relevant checks you ran.

## Git hooks

Enable the repository hooks after cloning:

```shell
git config core.hooksPath .githooks
```

Commit messages may not contain `Co-authored-by:` trailers. The canonical Dependabot co-author
trailer is the only exception. Pull requests run the same validation on GitHub, so the policy
applies even when a contributor has not enabled local hooks.
