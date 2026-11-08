# Datapoint Terminal

An AI-powered terminal: a desktop application for macOS, Linux and Windows where you work in a real shell, locally or over SSH, alongside an agent, powered by Anthropic, OpenAI or an open-weight model of your choice, that can talk with you, ask you questions and type into the terminal on your behalf, in plain sight.

## Status

Datapoint Terminal is in early development and hasn't been released yet; the first release will be 1.0.0-alpha.1, a test build. Continuous integration is set up to build and test it on macOS, Linux and Windows, and tagging a version builds installers for each (see [Packaging, signing and updates](docs/engineering.md#packaging-signing-and-updates)); they aren't signed yet, so macOS and Windows warn before opening them, and the application doesn't update itself yet. It's been used on macOS so far; Linux and Windows are supported by design but haven't been tried, and some features may not work there yet. Expect anything to change before version 1.0.

## Installing a test build

Test builds are on the [releases page](https://github.com/datapoint-software/terminal/releases), one set of files per system and processor: `arm64` for Apple silicon and ARM PCs, `x64` for Intel and AMD. They aren't signed yet, so each system warns before opening them; check that you downloaded them from that page before going past the warning.

- **macOS:** open `DatapointTerminal-osx-arm64-Setup.pkg` (or `osx-x64` on an Intel Mac). When macOS says it can't check it for malicious software, open System Settings › Privacy & Security, scroll to Security, choose Open Anyway and confirm. Do the same the first time you open Datapoint Terminal if macOS stops it again. `DatapointTerminal-osx-…-Portable.zip` holds the application to copy to Applications yourself instead.
- **Windows:** run `DatapointTerminal-win-x64-Setup.exe` (or `win-arm64`). When SmartScreen says it protected your PC, choose More info, then Run anyway. `DatapointTerminal-win-…-Portable.zip` runs without installing.
- **Linux:** download the `.AppImage` for your processor, make it executable (`chmod +x DatapointTerminal*.AppImage`) and run it. If it doesn't start, install FUSE 2 (`libfuse2` on most distributions, `libfuse2t64` on Ubuntu 24.04 and later).

The application doesn't update itself yet: to update, install the newer build over the old one. Settings and saved connections live in your user folder, not with the application, so they stay.

## Building and running

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.403 or later; `global.json` pins it).

```bash
dotnet build
dotnet run --project src/Datapoint.Terminal
```

The agent needs an API key for Anthropic or OpenAI, or the address of an OpenAI-compatible server running an open-weight model; add it in the application's settings. Keys are kept in the operating system's credential store.

Before your first commit, turn on the repository's Git hooks, which stop secrets and real host names or addresses from being committed. List any names of your own infrastructure that must never appear, such as host or customer names, one per line in `.git/info/forbidden`; that file stays in your clone.

```bash
git config core.hooksPath .githooks
```

To run the tests:

```bash
dotnet test
```

The SSH tests run against an OpenSSH server in a container. Without [Docker](https://www.docker.com/) they're skipped; to run them, build the server's image first:

```bash
docker build -t datapoint-terminal-sshd:test tests/ssh-server
```

## Documentation

- [Product](docs/product.md): what the application does, for whom, and the decisions taken so far, with the questions still open.
- [Engineering](docs/engineering.md): the technical stack, the terminal emulator's design and the repository conventions, with the decisions taken so far and the questions still open.
- [Autopilot notes](docs/autopilot.md): implementation decisions taken during development, kept for review.
- [Contributing](CONTRIBUTING.md): how to set up, make a change and open a pull request.
- [Code of conduct](CODE_OF_CONDUCT.md): how we treat each other, and where to report problems.
- [How we work](CLAUDE.md): the full rules for branch names, commit messages, pull requests and versions, followed by people and AI coding assistants alike.
- [Security policy](SECURITY.md): how to report a vulnerability privately.

## Using it safely

The agent works on real machines with your accounts, and what it reads goes to your AI provider. These habits keep you in control. The [known risks](#known-risks) below say where the application's own safeguards stop.

- **Start with a test build on machines you can rebuild.** Until version 1.0, try it on development or test machines before anything that matters.
- **Choose your provider knowingly.** While the agent works on a task, what it reads goes to the provider: terminal output, the files it opens, the workspace's instructions and host documentation. Check that the provider's terms allow that data. If it mustn't leave your network, use an open-weight model you run yourself. Each step in the chat shows exactly what was sent.
- **Start in Ask mode.** In Ask, every command waits for your approval. Move to Ask if risky or Plan once you know how the agent works. Use Ignore permissions only on machines you could lose, never alongside production terminals: in that mode every command runs without asking, in every terminal.
- **Label production hosts.** Mark each host as production, staging or development when you add it. Production tabs are colored, and every approval names the terminal and its environment, so you can see where a command will run before you approve it.
- **Read before you approve.** Check the command, the terminal it runs in and, for a plan, each step and how to roll it back. You can edit a command before approving it. If anything surprises you, say so in the message box: nothing the card proposed runs.
- **Stay at the keyboard.** Everything the agent types shows in the terminal as it happens. Typing in any terminal, or the Stop button, stops the agent at once. Don't leave it working on servers unattended.
- **Use accounts with only the rights the task needs.** Prefer a user without `sudo` where you can. A command run with `sudo` can read files only root can, and what it prints goes to the provider.
- **Keep secrets out of the chat.** When a program asks for a password, type it into the terminal, never into the message box: what you type at a password prompt is masked and never sent. Prefer SSH keys in your SSH agent to saved passwords. List files the agent must never read in a `.terminalignore` file (same syntax as `.gitignore`). Secret masking is a best guess, so when the application asks before sending a file that looks like it holds one, choose Send masked or skip it unless you're sure.
- **Check host keys.** The first time you connect to a host, compare its key's fingerprint with one you got another way, such as from the server's console or its administrator. If a known host's key changes, find out why before you connect.
- **Treat what the agent reads as untrusted.** Terminal output, files, other hosts and web pages can hold text written to steer the agent. Be most careful with approvals after it has read web pages, or files and output from machines you don't control. Trust a workspace only if you know who can change its files: its instructions guide the agent.
- **Have real backups.** The agent keeps a copy of each configuration file before editing it, but that's no substitute for backups or snapshots of the machines it works on.
- **Set a spending limit.** The usage display shows tokens and estimated cost, and View › Spending limit… sets a limit.
- **Review afterwards.** Session recordings show who typed what, you or the agent, and the agent records what it changed in each host's maintenance log.

## Known risks

- **Prompt injection.** Terminal output, files, remote hosts and web pages can hold text written to steer the agent. The application treats it as data, asks before risky commands and before each web request, flags text addressed to an AI, and asks before anything could carry what the terminals showed to another machine (product decision 106). These checks can't catch everything:
  - data the agent encodes (e.g. base64 or hex) before putting it in an address or a command isn't recognized as coming from the terminals;
  - network access from inside other programs (e.g. `python -c`, `node -e`) isn't recognized as a network command, though such commands still count as changes;
  - the check for text addressed to an AI finds only the common phrasings;
  - with Ignore permissions, nothing a command does is checked before it runs, in any terminal, production included (product decision 107).

  In Plan mode, with Ignore permissions, or with the web allowed always, review what the agent does after it has read web content.

- **Looking without asking.** Once the user allows the agent to look at some hosts before planning (product decision 111), its read-only commands there run without asking until the task ends. They show in the terminal as they run, but what they print, file contents included, goes to the AI provider, masked only for the secrets the application recognizes. Which commands count as read-only is the application's own check, from the command's text; a command it misjudges runs without asking too.
- **Saved login passwords.** A password saved for an SSH login (product decision 113) is in the system's credential store, so anyone who can unlock that store, or a program running as the user once it's unlocked, can read it. With "Allow always", connecting to that login no longer asks for anything, including when the agent opens the connection; the host key check still applies. Many people reuse a server's password elsewhere, which makes a saved one worth more than the server it unlocks.
- **Commands counted as read-only.** Which commands run without asking where only changes need approval is the application's own check, from the command's text against a shipped list and the commands the user taught (product decision 112). It doesn't see shell aliases or functions, so an alias named like a reading program passes, and a taught command passes with any option the list doesn't know writes (e.g. teaching `docker swarm ca` also lets `docker swarm ca --rotate` run unasked). Taught commands live only in the user's own settings, so a workspace's files can't add to them. A read run with `sudo` also runs unasked, so it can read files only root can, such as private keys or `/etc/shadow`, and send what it prints to the AI provider; it may also wait at a password prompt, where what the user types is masked.

## License

Datapoint Terminal is free software, licensed under the [GNU Affero General Public License, version 3](LICENSE) (AGPL-3.0), with additional terms in [NOTICE](NOTICE):

- You can use, change and share Datapoint Terminal. If you share a modified version, or let others use it over a network, you must offer them its source code under the same license.
- The application's legal notices, such as its About window, must keep the "Based on Datapoint Terminal by Datapoint" attribution and the Datapoint logo.
- Modified versions must be marked as modified, and the license grants no rights to the Datapoint name or logo beyond that attribution. See the [trademark policy](TRADEMARKS.md).

Datapoint also offers Datapoint Terminal under a commercial license, for organizations that can't accept the AGPL.

Contributors sign the [Contributor License Agreement](CLA.md) before their first contribution is merged. To report a security issue, don't open a public issue: follow the [security policy](SECURITY.md).

Copyright © 2026 Datapoint - Sistemas de Informação, Lda.
