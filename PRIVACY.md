# Privacy

This notice describes what Datapoint Terminal sends, to whom and when, and what it keeps on your computer. It covers the application as of version 1.0.0-alpha.1.

## In short

- **Datapoint receives nothing.** The application has no telemetry, analytics or crash reporting, and doesn't contact Datapoint's servers.
- **Your AI provider receives what the agent works with**, while it works on a task you gave it: your messages, terminal output and the files it reads. You choose the provider: Anthropic, OpenAI, or an OpenAI-compatible server, such as a model you run yourself.
- **Everything else stays on your computer**, apart from the web searches and pages you allow, and the hosts you connect to.

## What goes to your AI provider

Nothing is sent until you give the agent a task, or press Test in the provider settings, which sends one short message ("Reply with the single word OK.").

When you send a message, the agent's request includes:

- your message, and any excerpt you attached;
- the terminal output since the agent last read it, from the terminals open in the window;
- your instructions (`TERMINAL.md` in your home folder's `.datapoint-terminal` folder and in the workspace) and the agent's memory (`MEMORY.md`);
- the names and labels of the open terminals and connections, the hosts and skills the workspace documents, and the patterns in `.terminalignore`.

While it works, the agent also sends what its steps return: command output, the screen and working folder of a terminal, files it reads, host documents and maintenance log entries, history it searches and web pages it reads. Long conversations are summarized by the same provider. No screenshots, and no information about your computer beyond what the terminals show, are sent.

Before anything is sent:

- **Secrets it recognizes are masked**, replaced with "[secret removed]": private keys, common token formats (e.g. Anthropic, OpenAI, GitHub, Slack, AWS, Google, GitLab), `password=`-style assignments, passwords in addresses, and the secrets the application itself holds (your provider key, web search keys and SSH passwords). Masking is a best guess; it can miss a secret it doesn't recognize.
- **Passwords you type stay out.** The agent never types at a password prompt; it hands the terminal to you. SSH passwords are asked for in the application's own dialog and go only to the server.
- **Files listed in `.terminalignore` aren't read by the agent**, and commands that name them always ask first.
- **Instruction and memory files that look like they hold a secret pause** and ask whether to send them masked, as written, or not at all.
- Paths in your home folder are sent as `~/…`, so your account name isn't.

The provider's own terms and privacy policy apply to what it receives, including whether it keeps it or trains on it. Read them before you choose a provider, and check that they allow the data your terminals show. With a model on your own computer or network, nothing leaves it. The provider settings, setup and the chat each say where the agent sends your messages.

## Web searches and pages

The agent asks before every web search and every page it reads; you can allow once, always, or not at all. Even with "always", it asks again for an address it composed itself or one holding text from the terminals.

- A search sends only the query, with secrets masked, to the search you chose in Settings: your AI provider's own search, Brave, Tavily, or a SearXNG server you name.
- A page is fetched by the application itself, without cookies or saved logins, and only from public addresses. The page's server sees your IP address.
- Images in the chat are downloaded only when you click them; their server sees your IP address.

## Hosts you connect to

SSH connections go directly from your computer to the host you chose, possibly through the jump hosts in your SSH configuration. When the agent edits a file there, it first copies the original to `~/.datapoint-terminal/backups/` on that machine and prepares the new version in `~/.datapoint-terminal/staging/`. These copies aren't removed automatically.

## What's kept on your computer

| What | Where | For how long |
| --- | --- | --- |
| Settings: providers (without keys), workspaces, web search choices, saved login names (without passwords) | `settings.json` in the settings folder below | Until you change them |
| Your global memory and which instruction files were read (masked) | The settings folder | Until you change them |
| History: conversations, commands with their exit codes, the output sent to the agent, and the last output of closed terminals, masked | `history.db` in each workspace's data folder | Forever by default; set a limit in Settings › History |
| Session recordings: what each terminal showed, masked line by line, and who ran each command; not your keystrokes | `recordings/` in each workspace's data folder | The same limit as history; turn recording off in Settings |
| Known host keys | `host-keys.json` in each workspace's data folder, and `~/.ssh/known_hosts` when you choose to trust a host there | Until you remove them |
| Host documents and maintenance logs, which may hold host names, addresses and your account name | The workspace folder you chose (by default `~/.datapoint-terminal/default`) | Until you remove them |
| Provider keys, web search keys and saved SSH passwords | Your system's credential store: the macOS Keychain, Windows Credential Manager, or the Secret Service on Linux | Until you remove them in Settings |

The settings folder is `~/Library/Application Support/Datapoint Terminal` on macOS, `%APPDATA%\Datapoint Terminal` on Windows and `~/.config/Datapoint Terminal` on Linux. Workspace data folders are under `workspaces` in `~/Library/Application Support/Datapoint Terminal`, `%LOCALAPPDATA%\Datapoint Terminal` or `~/.local/share/Datapoint Terminal`. On macOS and Linux, `settings.json` and the recordings are readable only by your user.

You can clear history for one connection or all of it in the History window or Settings › History, delete recordings one at a time in the Recordings window, and edit or clear memory in Settings › Memory. Uninstalling the application doesn't remove these folders; delete them to remove everything.

The application keeps no log files.

## Updates

This version doesn't check for updates. When it does, it will ask GitHub, where releases are published, which will see your IP address.

## Who is responsible

Datapoint doesn't receive or process what you do with the application. You, or your organization if you use it for work, choose the AI provider and what it may receive, and the provider's terms govern that data. If what your terminals show includes personal data, such as customers' records or other people's details, make sure sending it to the provider you chose is allowed.

## Questions

Write to `hello@datapoint.software`. Changes to this notice are in this file's history.
