# Security policy

Datapoint Terminal runs shells on people's machines and servers, with their credentials, and lets an AI agent type into them. We take reports about its security seriously, and thank everyone who reports a problem responsibly.

## Supported versions

Datapoint Terminal hasn't been released yet. Until it is, security fixes go into the `main` branch only. Once there are releases, this section will say which versions receive fixes.

## Reporting a vulnerability

**Please don't report security problems in public issues, pull requests or discussions.**

Report them privately through GitHub: on this repository's **Security** tab, choose **Report a vulnerability**. Only you and the maintainers can see the report. GitHub explains how this works in [Privately reporting a security vulnerability](https://docs.github.com/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability).

A useful report says:

- what the problem is, and what an attacker could do with it;
- how to reproduce it: the steps, the operating system and shell, local or over SSH, and the AI provider and model, if they matter;
- the commit or version you found it in;
- any idea you have of how to fix it.

Please don't include real secrets, such as API keys or passwords, or details of a real infrastructure, in the report; made-up ones are enough to show the problem.

## What happens next

We'll confirm we received your report, look into it, and keep you informed until it's resolved. When a fix is ready, we publish a security advisory on this repository and credit you, unless you'd rather not be named. Please give us a reasonable time to fix the problem before you disclose it publicly, and tell us if you plan to.

## What counts

Examples of what we want to hear about:

- the agent running a command, or sending keys, without the user being able to see it in the terminal;
- text in terminal output, files, remote hosts or web pages (prompt injection) getting the agent past an approval, or widening what it's allowed to do;
- secrets (API keys, SSH keys, passwords, passphrases) stored outside the operating system's credential store, written to logs or crash reports, or sent to an AI provider;
- SSH host keys accepted without the user, or weaknesses in the SSH implementation or its use of cryptography;
- terminal contents sent to an AI provider, or anywhere else, without the user being told;
- ways to tamper with the application or its updates.

The limits listed under [Known risks](README.md#known-risks) in the README are known and documented; a report is still welcome if you find a way around them that goes further than what's described there. Problems in an AI provider's own service, or in the operating system, belong with them, though we'd like to know if Datapoint Terminal makes one worse.
