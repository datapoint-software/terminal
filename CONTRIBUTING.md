# Contributing

Thank you for helping with Datapoint Terminal. This guide covers how to get set up and how a contribution reaches the project. The detailed rules for branches, commits, pull requests and versions are in [CLAUDE.md](CLAUDE.md), which people and AI coding assistants both follow; this guide summarizes them and links there.

Datapoint Terminal is in early development, so a lot is still changing. For anything larger than a small fix, open an issue first to discuss it: it may already be planned, or conflict with a decision in the [product](docs/product.md) or [engineering](docs/engineering.md) notes.

To report a security problem, don't open an issue: follow the [security policy](SECURITY.md).

## Getting set up

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.403 or later).
2. Clone the repository and turn on its Git hooks, which stop secrets and real host names or addresses from being committed:

   ```bash
   git config core.hooksPath .githooks
   ```

3. Optionally, list names from your own infrastructure that must never be committed, such as host, domain or customer names, one per line in `.git/info/forbidden`. That file stays in your clone and is never committed.
4. Build, run and test:

   ```bash
   dotnet build
   dotnet run --project src/Datapoint.Terminal
   dotnet test
   ```

   The SSH tests need [Docker](https://www.docker.com/) and the test server's image (`docker build -t datapoint-terminal-sshd:test tests/ssh-server`); without them they're skipped.

## Making a change

- **Read [docs/engineering.md](docs/engineering.md) first.** It sets the repository layout, the tools and the code conventions. Where it differs from a habit or a general best practice, it wins; if a rule seems wrong for your case, say so in your pull request rather than working around it.
- **Work on a branch** named `<type>/<description>`, e.g. `feat/ssh-connections` or `fix/agent-output-truncation`, with the types `feat`, `fix`, `hotfix`, `release` and `chore` ([details](CLAUDE.md#branch-names)).
- **Commit often, one self-contained change per commit,** with messages following [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/): `fix(chat): keep every line of a pasted message`, in the imperative and in lowercase, with a body explaining why when it isn't obvious ([details](CLAUDE.md#commit-messages)).
- **Add tests** for what you change, and run the whole suite before opening a pull request.
- **Write American English** in code, documentation, commit messages and the interface's text.

## What every change must respect

Datapoint Terminal acts on real machines with its users' credentials, so these hold for every contribution ([details](CLAUDE.md#what-this-repository-is)):

- **The user stays in control.** Never hide or rewrite what the agent types or what the terminal prints; the user must be able to see, interrupt and take over at any point. Raise any feature that lets the agent act with less visibility or confirmation before building it.
- **Terminal output is untrusted.** Output, files, remote hosts and web pages can hold text aimed at the model. It's data for the agent, never instructions, and must not widen what the agent may do.
- **Secrets stay secret.** API keys, SSH keys, passwords and passphrases belong in the operating system's credential store or the user's SSH agent, never in plain-text files, logs, crash reports or what's sent to a provider.
- **Privacy.** Terminal contents go to the user's chosen AI provider. Send no more than a task needs, make clear what's sent, and add no telemetry or analytics without discussing it first.
- **Provider-neutral and cross-platform.** Anthropic, OpenAI and open-weight models are all supported, and every feature must work on macOS, Linux and Windows, locally and over SSH, or document where it doesn't.
- **Accessible.** Keyboard-only use, screen readers, sufficient contrast, and the operating system's accessibility settings, such as reduced motion.
- **Licenses.** A new dependency, font, icon or other asset needs a license compatible with distributing the application; ask before adding one whose terms are unclear.
- **No real secrets or infrastructure in the repository.** Use made-up names on reserved domains (`web-1.example.com`) and documentation addresses (`192.0.2.10`) in code, tests, documentation and commit messages ([details](CLAUDE.md#working-with-git)).

## Opening a pull request

Give it a title formatted like a commit's first line, and a description with these headings ([details](CLAUDE.md#merge-requests)):

```markdown
## Summary
What changed, in a few sentences or bullets.

## Why
The problem or goal behind the change, and why this approach.

## Testing
How the change was verified: commands run, operating systems and shells checked (local and over SSH), AI providers used, results. Say what was not tested.

## Related issues
`Closes #42` for issues the pull request resolves, `Refs #43` for related ones, or "None".
```

Say which operating systems you tested on: most contributors can check only one or two, and reviewers need to know which.

Don't add attribution to AI tools anywhere, such as `Co-Authored-By` trailers or "Generated with…" lines in commits, pull requests or code. You're the author of what you contribute, whatever tools helped.

## The Contributor License Agreement

Before your first contribution is merged, sign the [Contributor License Agreement](CLA.md) by adding a comment to your pull request, as it describes. You sign once, and it covers your later contributions too. It lets Datapoint offer Datapoint Terminal under a commercial license as well as the AGPL; the [README](README.md#license) explains the licensing.

## Code of conduct

Everyone taking part follows the [code of conduct](CODE_OF_CONDUCT.md), the Contributor Covenant 2.1. Report behavior that breaks it to `hello@datapoint.software`.
