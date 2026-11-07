# Datapoint Terminal

An AI-powered terminal: a desktop application for macOS, Linux and Windows where you work in a real shell, locally or over SSH, alongside an agent, powered by Anthropic, OpenAI or an open-weight model of your choice, that can talk with you, ask you questions and type into the terminal on your behalf, in plain sight.

This project is in early design. Nothing is built yet.

## Documentation

- [Product](docs/product.md): what the application does, for whom, and the decisions taken so far, with the questions still open.
- [Engineering](docs/engineering.md): the technical stack, the terminal emulator's design and the repository conventions, with the decisions taken so far and the questions still open.
- [Autopilot notes](docs/autopilot.md): decisions taken while the product owner was away, for review.

## Known risks

- **Prompt injection.** Terminal output, files, remote hosts and web pages can hold text written to steer the agent. The application treats it as data, asks before risky commands and before each web request, flags text addressed to an AI, and asks before anything could carry what the terminals showed to another machine (product decision 106). These checks can't catch everything:
  - data the agent encodes (e.g. base64 or hex) before putting it in an address or a command isn't recognized as coming from the terminals;
  - network access from inside other programs (e.g. `python -c`, `node -e`) isn't recognized as a network command, though such commands still count as changes;
  - the check for text addressed to an AI finds only the common phrasings;
  - with Ignore permissions, nothing a command does is checked before it runs, in any terminal, production included (product decision 107).

  In Plan mode, with Ignore permissions, or with the web allowed always, review what the agent does after it has read web content.

- **Looking without asking.** Once the user allows the agent to look at some hosts before planning (product decision 111), its read-only commands there run without asking until the task ends. They show in the terminal as they run, but what they print, file contents included, goes to the AI provider, masked only for the secrets the application recognizes. Which commands count as read-only is the application's own check, from the command's text; a command it misjudges runs without asking too.

## License

Datapoint Terminal is free software, licensed under the [GNU Affero General Public License, version 3](LICENSE) (AGPL-3.0), with additional terms in [NOTICE](NOTICE):

- You can use, change and share Datapoint Terminal. If you share a modified version, or let others use it over a network, you must offer them its source code under the same license.
- The application's legal notices, such as its About window, must keep the "Based on Datapoint Terminal by Datapoint" attribution and the Datapoint logo.
- Modified versions must be marked as modified, and the license grants no rights to the Datapoint name or logo beyond that attribution. See the [trademark policy](TRADEMARKS.md).

Datapoint also offers Datapoint Terminal under a commercial license, for organizations that can't accept the AGPL.

Contributors sign the [Contributor License Agreement](CLA.md) before their first contribution is merged.

Copyright © 2026 Datapoint - Sistemas de Informação, Lda.
