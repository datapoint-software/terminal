# Engineering

How Datapoint Terminal is built: the technical stack, how the code is organized, and the conventions it follows. What the application does is in [product.md](product.md).

The conventions follow each platform's own guidance; where we add or change a rule, it says so and why. The general .NET rules come from [Apiary's engineering conventions](https://github.com/datapoint-software/apiary/blob/main/docs/engineering.md), adapted to a desktop application. Choices that shape the whole project are in the [decision log](#decision-log); anything not settled yet is under [open questions](#open-questions).

Researched against official documentation and the projects' own sources on 2026-11-06, for .NET 10 with C# 14 and Avalonia 12.1. Check this file again before each major upgrade.

## Language

American English everywhere: our own names in code, documentation, commit messages and the interface's text. It's the language of the frameworks' names (`Color`, `Behavior`) and of the license text in `LICENSE`, which we can't change.

## Stack

| Concern | Choice | License |
| --- | --- | --- |
| Platform | .NET 10 (long-term support) and C# 14 | MIT |
| User interface | [Avalonia](https://avaloniaui.net/) 12.1 with `Avalonia.Skia` and `Avalonia.HarfBuzz` | MIT |
| View models | [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MIT |
| Terminal emulator | Our own (see [Terminal emulator](#terminal-emulator)) | |
| Pseudo-terminals | [Porta.Pty](https://github.com/tomlm/Porta.Pty), and Microsoft's out-of-box ConPTY on Windows (see [Pseudo-terminals](#pseudo-terminals)) | MIT |
| SSH | Our own implementation of the protocol, on vetted cryptography (see [SSH](#ssh)) | |
| AI providers | [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) over the official [Anthropic](https://github.com/anthropics/anthropic-sdk-csharp) and [OpenAI](https://github.com/openai/openai-dotnet) SDKs | MIT, Apache-2.0, MIT |
| Storage | Files, and SQLite through [Microsoft.Data.Sqlite](https://learn.microsoft.com/dotnet/standard/data/sqlite/) | MIT |
| Logging | Microsoft.Extensions.Logging with [Serilog's file sink](https://github.com/serilog/serilog-sinks-file) | MIT, Apache-2.0 |
| Packaging and updates | [Velopack](https://github.com/velopack/velopack) | MIT |
| Tests | xUnit v3 on Microsoft.Testing.Platform, `Avalonia.Headless.XUnit` | Apache-2.0, MIT |

## Repository layout

```
src/
  Datapoint.Terminal/              The executable: the Avalonia application, its views, view models and styles
  Datapoint.Terminal.Controls/     The terminal control: rendering, input, IME and accessibility
  Datapoint.Terminal.Emulator/     The emulator's core: parser, screen model, command records
  Datapoint.Terminal.Sessions/     Local shells and SSH: pseudo-terminals, connection profiles, shell integration scripts
  Datapoint.Terminal.Agent/        The agent: providers, the agent loop, approval modes, memory, host documentation
  Datapoint.Terminal.Security/     Credential storage, secret detection and redaction
tests/
  Datapoint.Terminal.*.Tests/      One test project per project in src/
  Datapoint.Terminal.Testing/      Shared builders and fakes
  Datapoint.Terminal.Architecture.Tests/   Checks the dependency rules below
  Datapoint.Terminal.Emulator.Fuzz/        Fuzzing targets for the parser and screen model
  Datapoint.Terminal.Emulator.Benchmarks/  BenchmarkDotNet benchmarks
tools/                             Repository scripts, such as the third-party notices generator
docs/  branding/
Datapoint.Terminal.slnx  Directory.Build.props  Directory.Packages.props  global.json  .editorconfig
```

- **Few projects, each with one reason to change.** A new project needs a dependency rule it enforces; otherwise the code goes into an existing one.
- **Inside a project, group by feature** (`Tabs/`, `Connections/`, `Memory/`), not by kind (`ViewModels/`, `Services/`).
- **Names:** projects and namespaces start with `Datapoint.Terminal`, matching the folder.

### Dependency rules

- `Emulator` depends on no project, and on no package outside .NET itself. It never references Avalonia.
- `Security` depends on no project.
- `Sessions` depends on `Emulator` and `Security`.
- `Agent` depends on `Emulator`, `Sessions` and `Security`. It never references Avalonia: everything the agent does can be tested without a display.
- `Controls` depends on `Emulator` only.
- `Datapoint.Terminal` (the application) references everything and is referenced by nothing but tests.

`Datapoint.Terminal.Architecture.Tests` fails the build when a project reference breaks these rules.

## .NET

### Code conventions

We follow Microsoft's conventions:

- [C# identifier names](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names):
  - PascalCase for types, members and constants, and an `I` prefix for interfaces.
  - `_camelCase` for private and internal instance fields, and `s_camelCase` for static fields.
  - camelCase for parameters and locals, and for primary constructor parameters of classes and structs. Records' primary constructor parameters are PascalCase.
- [C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions):
  - `var` only when the type is obvious from the right-hand side (`new`, a cast or a literal).
  - File-scoped namespaces, with `using` directives outside them.
  - Collection expressions, raw string literals, and `required` properties rather than constructors that only set fields.
- The [Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/) for public APIs, including the `Async` suffix.
- From the [.NET runtime's own style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md):
  - Visibility modifiers always written, and first.
  - No `this.`.
  - Classes `sealed`, or `static`, unless something derives from them.

Files:

- **One top-level type per file,** named after the type: `ScreenBuffer.cs` holds `ScreenBuffer`. Nested types stay in their parent's file.
- **The only exception is generic types.** A generic type's file is named without its type parameters, and a type extended by a generic one of the same name shares its file.
- **A partial type split across files** uses `Type.Purpose.cs`, such as `SessionHost.Logging.cs` for its `[LoggerMessage]` methods.
- Enforced by [Meziantou.Analyzer](https://github.com/meziantou/Meziantou.Analyzer) (MIT) rule MA0048.

Nesting and control flow (each level of nesting is one more condition the reader has to hold in mind):

- **At most three levels of nesting inside a method.** Every `if`, loop, `switch`, `try` and lambda body counts as a level.
- **Guard clauses first.** Handle invalid or uninteresting cases at the top and leave, so the rest of the method is the normal case.
- **No `else` after `return`, `throw`, `continue` or `break`.**
- **In loops, skip early with `continue`** instead of wrapping the body in an `if`. A body that still needs real logic becomes a well-named method.
- **`using` declarations** (`using var stream = …;`) instead of `using` blocks.
- **Pattern matching to merge checks** instead of nested ifs.
- **Switch expressions to map one value to another,** instead of `if`/`else if` chains. They're never nested inside each other.
- **No nested conditional operators.** `a ? b : c ? d : e` is easily misread.
- **Name complex conditions** (`var isPasswordPrompt = …;`) rather than nesting or negating a compound one.
- **Braces always,** even around a single statement.

Analyzers check what they can: Roslynator RCS1211 (`else` after a jump) and RCS1208 (reducible nesting), IDE0063 (`using` declarations), IDE0011 (braces), and CA1502 with a cyclomatic complexity limit of 10 per method in `CodeMetricsConfig.txt`. The three-level limit and nested conditional operators are checked in review: no open-source analyzer measures nesting depth.

**The parser's state machine is the one exception to the complexity limit.** A table-driven state machine reads best as one `switch` over states, as its specification describes it. `CA1502` is suppressed on that method only, with the reason next to the suppression.

Readability:

- **Names say what things are, in the domain's own words** (`ScrollRegion`, `CommandRecord`, `ConnectionProfile`), as full words. Only well-known abbreviations are allowed: `Id`, `Http`, `Ssh`, `Pty`, `Csi`, `Osc`, `Dcs`, `Sgr`, and the names of control sequences from their specifications (`Decstbm`, `Decsc`).
- **Methods are short,** but length isn't a hard limit. Split a method when the new one has a name that stands for real work, not to meet a line count.
- **No comments in code.** If code needs a comment to be understood, rename or restructure it until it doesn't. No commented-out code, and no XML documentation: we don't publish libraries for developers outside Datapoint.
  - **One exception, added for the emulator:** a handler that implements a control sequence links to its specification in one line (`// https://vt100.net/docs/vt510-rm/DECSTBM.html`). Terminal behavior is defined outside our code; the link tells the reader where the rules come from, and no name can carry that.
- **No side effects inside expressions:** no assignments, `++` or calls that change state in a condition or an argument. Use parentheses whenever `&&` and `||` are mixed.
- **Interfaces only at real boundaries:** where there are two implementations, or a seam such as the clock (`TimeProvider`), the pseudo-terminal, the SSH connection, the AI provider, the credential store or the file system. Not one interface per class, and no layers that only pass calls through.
- **No mocking library.** Tests use real objects, and small hand-written fakes at those boundaries.
- **Immutable by default:** records for data, `readonly` fields, `init` properties, and `IReadOnlyList<T>` and the like in signatures.
- **LINQ for short transformations without side effects.** Use a loop when the code changes something, exits early, or runs on the emulator's parsing or rendering path, where LINQ's allocations matter.
- **Booleans for states that are and will always be binary;** an enum when there could be a third state, or when several Boolean arguments would make a call unreadable. Literal arguments are named at the call site: `Resize(columns, rows, reflow: true)`.
- **The null-forgiving operator `!`** only where the compiler can't see that a value is set. Prefer restructuring so it isn't needed.
- **Async all the way down,** following [David Fowler's guidance](https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md): no `async void` (except Avalonia event handlers, which catch and log every exception), no `.Result` or `.Wait()`, `await` rather than `ContinueWith`, a `CancellationToken` through every call that takes one, and async factory methods instead of async work in constructors. Checked by CA2016, CA2012, and VSTHRD100 and VSTHRD002 from [Microsoft.VisualStudio.Threading.Analyzers](https://github.com/microsoft/vs-threading) (MIT).
- **Exceptions for bugs and infrastructure faults only.** Expected failures, such as a refused SSH login or a provider's rate limit, are results the caller handles. `catch (Exception)` only at boundaries: the application's entry points, each session's reader and writer loops, and the agent loop. Checked by CA1031.

[Primary constructors](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/instance-constructors#primary-constructors): positional records for data; a primary constructor for injected services, using its parameters directly; never assign to a primary constructor parameter; a normal constructor when it validates arguments or does more than receive them. IDE0290 is a warning, and CS9124 fails the build.

C# 14 features we use: `field`-backed properties, null-conditional assignment (`a?.B = x`), extension members, and `nameof` with unbound generic types.

### Performance on the hot path

The emulator parses everything a shell prints, and output can arrive at hundreds of megabytes per second. On that path, and only there, these rules come first:

- **No allocations per byte or per sequence.** Parse a `ReadOnlySpan<byte>` from pooled buffers (`ArrayPool<byte>` or `System.IO.Pipelines`); parameters go into a fixed buffer, strings into a reused one, decoded only when dispatched.
- **Find printable runs with `SearchValues<byte>`** and `IndexOfAny`, which is vectorized, and write them straight into the row; fall back to the state machine only around control characters and non-ASCII.
- **Cells are small structs,** with styles, graphemes and hyperlinks stored once per page and referenced by index.
- **Measure before optimizing,** with the benchmarks in `Datapoint.Terminal.Emulator.Benchmarks`, and keep the result in the commit message.

### Enforcement

- `.editorconfig` starts from [the dotnet/docs repository's](https://github.com/dotnet/docs/blob/main/.editorconfig). Our changes to it are marked with a comment that says why: configuration files aren't code, and the reason isn't visible anywhere else.
- `Directory.Build.props`: `Nullable` and `TreatWarningsAsErrors` on; `AnalysisLevel` pinned to `10.0-recommended`; `EnforceCodeStyleInBuild` on; `IsAotCompatible` on every project except the application (see [Native AOT](#native-aot-and-trimming)).
- [Meziantou.Analyzer](https://github.com/meziantou/Meziantou.Analyzer) (MIT) and [Roslynator](https://github.com/dotnet/roslynator) (Apache-2.0) run in every project, with the rules we adopt at warning severity.
- **No SonarAnalyzer.** Version 10 is under the SONAR Source-Available License, which isn't open source.
- `dotnet format --verify-no-changes` runs in CI.
- `BannedSymbols.txt` fails the build on APIs we don't use: `BinaryFormatter`, `System.Random` for anything security-related, `Type.GetType` and `Activator.CreateInstance` (they break trimming and hide dependencies), and `ReflectionBinding`.

### Build and packages

- `.slnx` solution. `global.json` pins the .NET 10 SDK and selects Microsoft.Testing.Platform as the test runner.
- Central package management in `Directory.Packages.props`, with lock files, restore in locked mode in CI, and NuGet audit of transitive packages.
- **Pinned versions with a reason** carry a comment in `Directory.Packages.props` linking the issue, so automated updates don't undo them (see [Tests](#tests) for `xunit.v3`).
- **Third-party notices are generated,** as in Apiary: after adding or upgrading a package, regenerate `THIRD-PARTY-NOTICES.txt` and commit it with the change; CI fails when it's out of date. The generator accepts only reviewed licenses: MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause and the Unicode License v3 (for the Unicode data the emulator's width tables are built from). A package under another license fails it until someone has reviewed that license, including whether it still lets Datapoint offer Datapoint Terminal under a commercial license as well as the AGPL.
- **Borrowed code is recorded too.** Code adapted from another project (e.g. VS Code's shell integration, Git Credential Manager's interop) keeps its copyright notice in the file it went into, and is listed in `THIRD-PARTY-NOTICES.txt`.

### Native AOT and trimming

- **Our code is AOT-clean from the start:** `IsAotCompatible` on every project but the application, compiled bindings only, System.Text.Json source generation, `[LibraryImport]` for platform calls, no reflection-based view location.
- **We ship self-contained JIT builds with ReadyToRun for now.** The Anthropic SDK isn't AOT-compatible yet ([anthropic-sdk-csharp#79](https://github.com/anthropics/anthropic-sdk-csharp/issues/79)). A CI job publishes with Native AOT on each platform to keep everything else free of warnings, so switching costs nothing once that's fixed and startup has been measured.

## Avalonia

### Version and packages

- **Avalonia 12.1** with `Avalonia.Skia` and `Avalonia.HarfBuzz`; the application calls `.UseHarfBuzz()`, without which Avalonia 12 refuses to start. Avalonia 11 isn't used.
- **The default X11 backend on Linux,** which also runs under Wayland through XWayland. Avalonia's native Wayland backend is opt-in and early-stage; we revisit it when it's stable.
- **Only Avalonia's open-source packages.** Not used:
  - `AvaloniaUI.DiagnosticsSupport`, which the official templates add: it has no open-source license. Remove it from generated projects.
  - Avalonia's paid tooling and components (DevTools, Parcel, the Pro controls).
- See [Avalonia 12's breaking changes](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes) before following any older example.

### Application structure

- **MVVM with [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)**, the default of Avalonia's templates. Its source generators (`[ObservableProperty]`, `[RelayCommand]`) keep view models short, plain C#, and work with Native AOT. Not ReactiveUI: its reactive style is harder to read for anyone who doesn't already know it.
- **Compiled bindings everywhere,** the default in Avalonia 12. Every view and data template declares `x:DataType`, and `ReflectionBinding` is banned.
- **An explicit view locator:** a `switch` from each view model type to its view (`TabViewModel vm => new TabView()`). The templates' locator finds views by name through reflection, which breaks trimming and hides mistakes until run time.
- **Dependency injection with Microsoft.Extensions.DependencyInjection,** set up in `App.OnFrameworkInitializationCompleted`, as [Avalonia's guide](https://docs.avaloniaui.net/docs/app-development/dependency-injection) shows. Views take no services; view models do.
- **Platform services through small interfaces:** the clipboard, file pickers and launcher come from `TopLevel` and are wrapped, so view models can be tested without a window.
- **View models don't touch `Dispatcher`.** Work that must reach the UI thread goes through an injected abstraction, so view models test without Avalonia.
- **XAML (`.axaml`) for layout and styles;** plain C# for the terminal control, which draws itself and has no template.

### Styling and themes

- **`FluentTheme`, following the operating system's light or dark setting** (`RequestedThemeVariant="Default"`).
- **Colors are tokens** in `ResourceDictionary.ThemeDictionaries` (`Light`, `Dark`), used through `DynamicResource`. No color is written in a view.
- **`ControlTheme` to restyle a control; `Styles` with selectors only for small, app-wide adjustments** such as `:focus-visible`.
- **Our own high-contrast theme.** FluentTheme has none in Avalonia 12.1. The application switches to it when the operating system asks for high contrast, which Avalonia reports on Windows and macOS; on Linux, where it doesn't, the user chooses it in settings.
- **The terminal's color scheme is data,** not XAML: a typed model (the 16 ANSI colors, foreground, background, cursor, selection) that users edit and that changes at run time.

### The terminal control

- **A `Control` that overrides `Render`** and draws with `DrawingContext`: rectangles for backgrounds and selection, `DrawGlyphRun` for each run of cells with the same style. It works the same on every platform and in headless screenshot tests. We draw directly with SkiaSharp (`ICustomDrawOperation`) only if profiling shows that this path is the bottleneck.
- **The grid decides widths, not the font.** The cell size comes from the font's metrics, and each cell's width comes from the emulator's Unicode tables; the shaper only draws what fits.
- **Shaped runs are cached per row** and rebuilt only when the row changes. Positions are snapped to device pixels using the window's render scaling.
- **Never render per byte.** The reader thread feeds the emulator and marks rows dirty; the control asks for at most one frame per display refresh (`TopLevel.RequestAnimationFrame`) and renders from a snapshot of the dirty rows.
- **Keyboard:** `KeyDown` for keys that become control sequences, `TextInput` for typed text. The control takes Tab and arrow keys itself instead of letting them move focus.
- **Input methods:** a `TextInputMethodClient` subclass that shows the composition at the cursor, modeled on Avalonia's own `TextBoxTextInputMethodClient`.
- **Clipboard** through Avalonia 12's asynchronous API (`TryGetTextAsync`, `SetValueAsync`).

### Accessibility

- **The terminal control has an automation peer** (`OnCreateAutomationPeer`) that exposes the visible text and scrolling, and new output is announced through a polite live region. The chat pane announces the agent's messages the same way.
- **Known gap:** Avalonia 12.1 has no text pattern for custom controls, so screen readers can't move through the terminal's text line by line or word by word, as they can in Windows Terminal and macOS Terminal ([Avalonia#10143](https://github.com/AvaloniaUI/Avalonia/issues/10143)). For the first release, the automation peer and live regions, and an early test of a hidden read-only text box that mirrors the screen; in parallel, we contribute a text pattern to Avalonia (decision 11).
- **Every interactive element is reachable by keyboard** and has an accessible name; `AutomationId`s stay stable across languages.

### Translations

- **Text comes from `.resx` resources,** never written in a view or view model.
- **Switching language without restarting** goes through a small `Localizer` service that bindings read from and that notifies them when the language changes. Avalonia's documented `x:Static` pattern doesn't update at run time.
- **English only, for now** (product decision 70). Adding a language means adding its catalog.

## Terminal emulator

The emulator is the core of the product: the user sees it and the agent works through it.

### Design

- **Core separate from display.** The emulator's core (the escape-sequence parser and the screen model) is a plain .NET library with no dependency on Avalonia. The Avalonia control only draws it and passes input to it. The core can be tested headless, quickly and without a display.
- **One screen model.** The user's view and the agent both read the same screen model, in the same process, so the agent never sees something different from the user.
- **Built for the agent.** The core tracks what an agent needs, as part of the model rather than added on top:
  - **Command records** from shell integration markers: each command's prompt, input and output rows, its command line, exit code, working folder and start and end times.
  - **Who caused what:** whether the user or the agent typed each command. This comes from our own input path, which knows who wrote to the terminal, never from escape sequences, which any program can print.
  - **Changes since the agent's last read:** every change to a row stamps it with an increasing sequence number, and the agent asks for rows changed since the last number it saw, as WezTerm's model does.
  - **Screen state:** the alternate screen used by full-screen programs, and modes such as bracketed paste, mouse reporting and synchronized output.
  - **Visible text as well as stored text.** Invisible text, text in the same color as its background and overwritten text are flagged, since the user can't see them but the agent could read them.

### Specifications

In this order of priority:

1. [Paul Williams' DEC ANSI parser](https://vt100.net/emu/dec_ansi_parser) for the parser's state machine and its error recovery. We implement it from the description; the page's diagram and text are under a Creative Commons license and aren't copied. [Alacritty's `vte`](https://github.com/alacritty/vte) (MIT or Apache-2.0) is a clean implementation to compare with.
2. [xterm's control sequences](https://invisible-island.net/xterm/ctlseqs/ctlseqs.html) for what modern programs expect.
3. The [DEC VT510 manual](https://vt100.net/docs/vt510-rm/contents.html) for exact behavior: margins, origin mode, saving the cursor, tabs, erasing.
4. [ECMA-48](https://ecma-international.org/publications-and-standards/standards/ecma-48/) for names and the catalog of control functions.
5. [Ghostty's terminal reference](https://ghostty.org/docs/vt) as a list of what real applications use today.

We identify as `TERM=xterm-256color`, with `COLORTERM=truecolor` and `TERM_PROGRAM=DatapointTerminal`, and answer `XTVERSION` with our name and version. A terminal type of our own would break programs on SSH hosts that don't know it; extra features are offered by answering queries (`DA1`, `DA2`, `DECRQM`, `XTGETTCAP`) instead.

### Parser and screen model

- **The parser knows nothing about the screen.** It's the Williams state machine over bytes, calling an interface (`Print`, `Execute`, `CsiDispatch`, `EscDispatch`, the OSC, DCS and APC callbacks).
- **UTF-8 is decoded at the parser's edge, in the ground state only,** keeping state across reads, since a character can be split between them. Bytes 0x80 to 0x9F aren't treated as C1 controls. Invalid bytes become U+FFFD.
- **Everything is bounded:** 32 parameters with sub-parameters, 2 intermediate bytes, 64 KB for an OSC string by default, and per-handler limits for DCS and APC data. A string ends at BEL or ST and is dropped on CAN or SUB.
- **Width and graphemes from our own Unicode tables,** generated from the current Unicode Character Database rather than taken from .NET, which lags a version behind. Graphemes are clustered following [UAX #29](https://www.unicode.org/reports/tr29/) and [mode 2027](https://github.com/contour-terminal/terminal-unicode-core), with a classic `wcwidth` mode for compatibility.
- **Lines remember whether they wrapped,** so the primary screen and its scrollback reflow when the window is resized. The alternate screen never reflows and has no scrollback.
- **Scrollback is a ring of fixed-size pages,** each with its own tables of styles, graphemes and hyperlinks, and is limited in bytes rather than lines.
- **Selection and search work on logical lines** and on positions that survive scrolling, and belong to the core, so the agent can use them too.

### Features

First release:

- 256 colors and true color (both the `;` and `:` forms), styled and colored underlines.
- Bracketed paste, focus events, mouse reporting with SGR coordinates (1000, 1002, 1003, 1006), the alternate screen (1049), synchronized output (2026).
- Window title, working folder (OSC 7), hyperlinks (OSC 8), writing to the clipboard (OSC 52), color queries (OSC 4, 10, 11, 12).
- Shell integration markers: OSC 133, VS Code's OSC 633, and iTerm2's OSC 1337 for the working folder and marks.
- Terminal reports: `DA1`, `DA2`, `DECRQM`, `XTVERSION`, cursor position.
- The [Kitty keyboard protocol](https://sw.kovidgoyal.net/kitty/keyboard-protocol/), which modern terminal programs increasingly use. Implemented from its documentation: kitty's code is GPL and isn't copied.

Later: Kitty graphics, Sixel, reading the clipboard through OSC 52, left and right margins, notifications (OSC 9 and 777) and progress (OSC 9;4). Never: ReGIS, font changes through OSC 50.

### Security

Following [David Leadbeater's survey of terminal security](https://dgl.cx/2023/09/ansi-terminal-security):

- **No reply ever repeats what a program sent.** Reporting the window title is off; `DECRQSS` answers only valid requests, as a real VT520 does; OSC 50 isn't supported. Each of these has led to keystroke injection in other terminals.
- **Reading the clipboard through OSC 52 asks the user;** writing to it is allowed.
- **No escape sequence makes the application touch the network,** look up a host name or run anything.
- **Lengths are capped** (titles, links, OSC data) and title changes are rate-limited.
- **Pasting is protected:** escape characters are removed from pasted text, and pasting several lines without bracketed paste asks first.
- **Shell integration markers can be faked** by any program's output. The command line reported by OSC 633 is trusted only with the session's secret nonce, as VS Code does; other markers are hints, never a basis for a security decision.
- **What the agent reads is untrusted** (decision in [CLAUDE.md](../CLAUDE.md)): it's passed to the model as data, and flagged when hidden from the user.
- **What the agent types is checked:** escape characters in its commands are refused, so a model can't send control sequences into the terminal.

### Pseudo-terminals

- **[Porta.Pty](https://github.com/tomlm/Porta.Pty)** (MIT), maintained and built for .NET 10 with Native AOT, starts local shells; we study it first and adopt or fork it. Microsoft's `vs-pty.net` isn't used: it has had no code changes since 2021.
- **On Windows, Microsoft's out-of-box ConPTY** (`Microsoft.Windows.Console.ConPTY`, MIT, from the Windows Terminal project), which passes control sequences through more faithfully than the one built into Windows, with grapheme widths turned on so its idea of a character's width matches ours. The built-in one is the fallback.
- **On macOS and Linux, never `fork()` from .NET,** whose runtime is multi-threaded: a small native helper starts the shell, as node-pty and Porta.Pty do.
- **We wait for our own child processes** (`waitpid` on each one), since .NET only reaps processes it started itself.
- **Password prompts are detected locally** by checking whether the terminal has hidden typing (canonical mode on, echo off), the heuristic Ghostty and WezTerm use. It can't work over SSH, where the remote side changes the setting, so there the agent relies on the prompt's text and on what it asked the user for (product decisions 16 to 19).

### Shell integration scripts

- **Our own scripts,** loaded when the shell starts without changing any of the user's files, using VS Code's technique: `--init-file` for bash, a temporary `ZDOTDIR` for zsh, `--init-command` for fish, `-NoExit -Command` for PowerShell. Each script loads the user's own configuration first.
- **VS Code's scripts are MIT** and may be adapted, with their notice kept. Ghostty's bash and zsh scripts are GPL, despite Ghostty itself being MIT, and so are iTerm2's and kitty's: none of them is copied, only the ideas.
- **The scripts emit** OSC 133 (A, B, C, D), OSC 633 with a nonce passed to the shell in an environment variable, and OSC 7.

### Reference projects

Reading these is encouraged; copying from them follows their license.

| Project | License | Use |
| --- | --- | --- |
| [RoyalTerminal](https://github.com/royalapplications/RoyalTerminal) | MIT | Evaluated, and not used (decision 9). |
| [xterm.js](https://github.com/xtermjs/xterm.js), [Ghostty](https://github.com/ghostty-org/ghostty) (except its shell scripts), [WezTerm](https://github.com/wezterm/wezterm), [Alacritty](https://github.com/alacritty/alacritty), [Windows Terminal](https://github.com/microsoft/terminal), [foot](https://codeberg.org/dnkl/foot), [Contour](https://github.com/contour-terminal/contour), [SwiftTerm](https://github.com/migueldeicaza/SwiftTerm), [XtermSharp](https://github.com/migueldeicaza/XtermSharp), [node-pty](https://github.com/microsoft/node-pty) | MIT or Apache-2.0 | Design and behavior references. |
| kitty, iTerm2 and its shell integration, esctest | GPL | Never copied. esctest may be run against our application from outside. |

### Acceptance criteria

The emulator is ready for daily use when it passes both lists, on macOS, Linux and Windows.

Compatibility:

- [`vttest`](https://invisible-island.net/vttest/), and everyday use of `vim`, `htop`, `tmux` and `less`.
- Unicode, including wide (CJK) characters and emoji.
- Input methods (IME) for languages such as Chinese and Japanese.
- Mouse modes, bracketed paste and true color.
- Fast rendering when large output scrolls past (e.g. `cat` of a 100 MB file).
- Screen-reader access on each platform.

Agent:

- One screen model shared by the user's view and the agent.
- Who caused each command and its output.
- Shell integration markers for bash, zsh, fish and PowerShell.
- Changes since the agent's last read.

## SSH

- **Our own implementation of the SSH protocol,** with no SSH library (decision 7); the system's `ssh` is never used (decisions 4 and 5).
- **We implement the protocol, never the cryptography,** following the rules under [Cryptography](#cryptography), including FIPS mode.
- **An external security review** by a firm specializing in cryptography and protocols, booked as soon as the transport and login work rather than just before release, so there's time to fix what it finds (decision 10).
- **Specifications:** the SSH RFCs ([4250](https://www.rfc-editor.org/rfc/rfc4250) to [4254](https://www.rfc-editor.org/rfc/rfc4254), [4256](https://www.rfc-editor.org/rfc/rfc4256), [4419](https://www.rfc-editor.org/rfc/rfc4419), [8308](https://www.rfc-editor.org/rfc/rfc8308), [8332](https://www.rfc-editor.org/rfc/rfc8332), [8709](https://www.rfc-editor.org/rfc/rfc8709), [8731](https://www.rfc-editor.org/rfc/rfc8731)), the [SFTP draft version 3](https://datatracker.ietf.org/doc/html/draft-ietf-secsh-filexfer-02), and OpenSSH's own [`PROTOCOL` files](https://github.com/openssh/openssh-portable/blob/master/PROTOCOL), which document its extensions (certificates, the agent protocol, security keys, strict key exchange).
- **Only modern algorithms by default,** as current OpenSSH offers them, with strict key exchange against the Terrapin attack ([CVE-2023-48795](https://terrapin-attack.com/)). Older algorithms are enabled per host only when `~/.ssh/config` asks for them.
- **Tested hard:** interoperability tests against current OpenSSH servers and agents in containers, published test vectors for every algorithm, fuzzing of every packet parser, and an independent security review before the first release.
- **Built for the agent** (product decisions 57 and 58). Owning the connection lets the agent:
  - run checks on separate exec channels, with real exit codes and standard error kept apart;
  - read and write files exactly through SFTP, for edits as diffs and backups;
  - start the session's shell with shell integration already loaded;
  - keep one connection per host for the group tab, with checks running in parallel;
  - notice why a connection dropped, reconnect and resume;
  - survey hosts for their documentation without typing into the terminal.

  Every action on a channel other than the terminal is reported to the chat as it happens, recorded in session recordings, and goes through the same approval modes as typed commands.
- **The application reads `~/.ssh/config` itself,** following OpenSSH's rules (first value wins, `Include`, `Host` and `Match`), and the user's `known_hosts`.
- **Host-key confirmations, passwords and passphrases appear in the application's own windows,** never in the terminal.
- **A setting the application doesn't support yet fails clearly,** naming it.
- **Order of work** after the transport, login and sessions: reading `~/.ssh/config`, jump hosts, SSH agent support (which also covers hardware security keys held by the agent), file transfer, port forwarding, certificates, agent forwarding, then Kerberos and smart cards (decision 13).

## Cryptography

Checked against Microsoft's [cross-platform cryptography matrix](https://learn.microsoft.com/en-us/dotnet/standard/security/cross-platform-cryptography) (updated 2026-08-03). .NET 10 on its own can't support modern SSH: it has no X25519 or Ed25519 ([dotnet/runtime#14741](https://github.com/dotnet/runtime/issues/14741)), ML-KEM only on Linux with OpenSSL 3.5 and on Windows Insider builds, and no raw ChaCha20 or Poly1305.

### Rules

- **We never implement a cryptographic primitive.** Ciphers, key exchange, signatures, hashes, key derivation and random numbers come from a vetted implementation; what we write is the protocol around them. **One documented exception:** `bcrypt_pbkdf`, the key derivation OpenSSH uses for its encrypted key files, which neither .NET nor BouncyCastle provides. It's ported from OpenBSD's reference implementation (ISC license, notice kept) onto BouncyCastle's Blowfish, verified against OpenSSH's test vectors, and never used in FIPS mode (decision 14).
- **Two sources, in this order:**
  1. **.NET's built-in cryptography** (`System.Security.Cryptography`), wherever it works on all three platforms. It uses the operating system's libraries, which the OS vendor patches and which are FIPS 140 validated.
  2. **[BouncyCastle](https://github.com/bcgit/bc-csharp)** (MIT) for the rest: X25519, Ed25519, ChaCha20 and Poly1305, ML-KEM, AES-CTR.
- **One place for it.** All cryptography goes through a `Cryptography` area in `Datapoint.Terminal.Security`. Each algorithm SSH can negotiate maps to exactly one implementation there, marked with its source and whether it's FIPS approved. Nothing else references `System.Security.Cryptography` or BouncyCastle; `Datapoint.Terminal.Architecture.Tests` checks it. When .NET covers an algorithm on every platform, its implementation moves from BouncyCastle to .NET in that one place.
- **Proven against published vectors:** each algorithm is tested against its RFC's test vectors and Google's [Project Wycheproof](https://github.com/C2SP/wycheproof) (Apache-2.0), which targets known implementation mistakes, and the whole against real OpenSSH servers and agents.
- **Secrets in memory:** compared with `CryptographicOperations.FixedTimeEquals`, wiped with `CryptographicOperations.ZeroMemory` after use, and never logged.
- **Banned:** `System.Random` for anything security-related; MD5 and SHA-1 except where a protocol requires them for something other than security, such as a legacy host's fingerprint display.

### Where it's used

| Use | Implementation |
| --- | --- |
| SSH key exchange | `mlkem768x25519-sha256`, `curve25519-sha256`: BouncyCastle. `ecdh-sha2-nistp256/384/521`: .NET |
| SSH host and user keys | Ed25519: BouncyCastle. ECDSA on NIST curves, and RSA with SHA-2 (`rsa-sha2-256/512`): .NET. SHA-1 RSA (`ssh-rsa`) only when a host's configuration asks for it |
| SSH encryption and integrity | AES-GCM, HMAC-SHA2: .NET. `chacha20-poly1305@openssh.com`, AES-CTR: BouncyCastle |
| Encrypted private key files | OpenSSH's format: `bcrypt_pbkdf` ported from OpenBSD onto BouncyCastle's Blowfish, then AES from .NET. PKCS#8 keys: PBKDF2 from .NET |
| HTTPS to AI providers | .NET's `HttpClient` with the operating system's TLS and trusted certificates, so corporate proxies and certificate policies keep working. No TLS of our own, and certificate checks are never turned off |
| Saved API keys and passwords | The operating system's credential store (see [Security and credentials](#security-and-credentials)) |
| Data at rest, if ever needed | AES-GCM from .NET, with its key in the credential store |
| Nonces and identifiers | `RandomNumberGenerator` |

### FIPS mode

Enterprise and government customers may require FIPS 140 validated cryptography (product decision 59).

- **In FIPS mode, only .NET's built-in cryptography is used,** which comes from the operating system's validated modules. BouncyCastle is never called; the `Cryptography` area refuses any algorithm not marked FIPS approved.
- **FIPS mode turns on by itself when the operating system is in FIPS mode** (Windows' FIPS policy, Linux's `/proc/sys/crypto/fips_enabled`), and can also be turned on in settings. macOS has no system-wide FIPS mode, so there it's turned on in settings; Apple's validated module (corecrypto), which .NET uses on macOS, is accepted for FIPS. The application shows when it's on.
- **What remains available over SSH:** key exchange on NIST curves, ECDSA and RSA (SHA-2) keys, AES-GCM, HMAC-SHA2. Ed25519 keys, Curve25519, ChaCha20-Poly1305 and, for now, ML-KEM are not, since .NET doesn't provide them from a validated module on every platform. ML-KEM becomes available where .NET supports it (Linux with OpenSSL 3.5).
- **Private keys in FIPS mode** come from the SSH agent, which does its own signing, or from PKCS#8 files protected with PBKDF2. OpenSSH's own encrypted key format relies on `bcrypt_pbkdf`, which isn't FIPS approved.
- **A host that offers only algorithms outside this set fails with a clear message** naming what it offered and what FIPS mode allows.
- **Our wording is careful:** Datapoint Terminal *uses FIPS 140 validated cryptographic modules from the operating system* in FIPS mode. The application itself isn't validated, and we never claim it is.

## The agent and AI providers

- **One common interface: `IChatClient` from Microsoft.Extensions.AI.** The Anthropic SDK provides its own; OpenAI and OpenAI-compatible servers go through `Microsoft.Extensions.AI.OpenAI`. Messages, tools, streaming and token usage look the same whichever provider is behind them.
- **Our own agent loop,** not an agent framework. Approval before each risky command, secret redaction before each request, output limits, interruption and streaming into the chat are the product; an explicit loop of a few hundred lines shows them more clearly than Microsoft Agent Framework or Semantic Kernel would hide them. Tools are described with `AIFunction`.
- **Chat Completions for OpenAI-compatible servers,** and the Responses API only for OpenAI itself: support for the Responses API varies between servers.
- **Tolerant of weaker models and servers:** tool-call arguments are validated, and a malformed call is answered with an error the model can correct, never an exception. Missing token counts are shown as unknown.
- **Anthropic's prompt caching** is used through the SDK's own `IChatClient`, which is the only one that understands it.
- **The Anthropic SDK is pinned and kept behind our adapter.** It publishes breaking major versions often.

## Security and credentials

- **Credentials go to the operating system's store** through thin wrappers of our own: Keychain on macOS, the Secret Service on Linux, Credential Manager on Windows (DPAPI-protected files when Credential Manager isn't available, such as over a remote login). They're modeled on [Git Credential Manager](https://github.com/git-ecosystem/git-credential-manager)'s interop (MIT), with its notice kept, and use `[LibraryImport]`.
- **On Linux without a Secret Service,** such as a headless machine, the application says so and offers, in order: `pass`, environment variables (`ANTHROPIC_API_KEY`, `OPENAI_API_KEY`), a key file the user names, and, only with explicit consent and a visible warning, a file readable only by the user. It never falls back silently.
- **Secret detection before anything reaches a provider** combines:
  - rules ported from [gitleaks](https://github.com/gitleaks/gitleaks) (MIT, with its notice kept), run with `RegexOptions.NonBacktracking` and a timeout, since they were written for Go's regular expressions;
  - [Microsoft.Security.Utilities.Core](https://github.com/microsoft/security-utilities) (MIT) for well-known key formats;
  - exact matching of secrets the application knows, such as the user's own API keys;
  - an allowlist the user can edit.

  Not used: TruffleHog (AGPL, which would rule out the commercial license) and secrets-patterns-db (CC-BY-SA).

## Storage

- **Locations** follow each operating system: `Environment.SpecialFolder.ApplicationData` for settings and `LocalApplicationData` for data, which .NET maps to `%APPDATA%` and `%LOCALAPPDATA%` on Windows, `~/Library/Application Support` on macOS, and the XDG folders on Linux. Logs and caches go to `~/Library/Logs` and `~/Library/Caches` on macOS, and `XDG_STATE_HOME` and `XDG_CACHE_HOME` on Linux.
- **Settings:** one JSON file, read and written with System.Text.Json source generation.
- **Memory and host documentation:** Markdown files, so users can read, edit, search and version them (product decision 38). Host documentation goes to a folder the user can change in settings, by default one folder per connection profile in the application's data folder (product decision 74). The application watches it and reloads files changed outside it.
- **Conversations and history:** SQLite through Microsoft.Data.Sqlite, in WAL mode, with full-text search.
- **At rest,** files are readable only by the user, and we rely on the operating system's disk encryption. Free builds of encrypted SQLite are no longer available; if a column needs encrypting, it's encrypted with AES-GCM and a key kept in the credential store.

## Logging and crash reports

- **Microsoft.Extensions.Logging** with `[LoggerMessage]` source-generated methods, written to rolling, size-limited files through Serilog's file sink, configured in code.
- **Logs never contain secrets, terminal contents or prompts.** They're for diagnosing the application, not for recording what the user did.
- **Crashes are reported locally only:** unhandled exceptions write a report (exception, version, operating system, the last log lines, with secrets redacted), and on the next start the user can copy it or open a GitHub issue with it. Nothing is sent automatically (CLAUDE.md: no telemetry).

## Tests

- **[xUnit v3](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform) on Microsoft.Testing.Platform,** with xUnit's own assertions. No FluentAssertions, which is now commercial.
- **xUnit v3 is pinned to 3.2.x.** `Avalonia.Headless.XUnit` 12.1 fails with xUnit v3 4.x ([Avalonia#22072](https://github.com/AvaloniaUI/Avalonia/issues/22072)); the pin carries a comment linking the issue. We reference its `xunit.v3.mtp-v2` package, which runs on Microsoft.Testing.Platform 2: the code coverage extension needs it, and the plain `xunit.v3` package still brings version 1.
- **A test project is added with its first tests,** since Microsoft.Testing.Platform fails a run in which a test project has none.
- **Tests read as specifications:** one behavior per test, arrange, act, assert, with no `if` or loops. Data comes from small named builders, not unexplained literals. Test method names are sentences with underscores (`Wraps_text_at_the_right_margin`); CA1707 is off for test projects only.
- **Coverage** with Microsoft.Testing.Extensions.CodeCoverage. CI fails when a project falls below its threshold; raise a threshold when coverage grows, and never lower one without saying why in the commit.
- **The emulator** is tested in layers:
  - **Unit tests per control sequence,** written from the specifications.
  - **Golden tests,** the main investment: recorded output of real programs (`vim`, `htop`, `tmux`, `less`, shells) is replayed through the core, and the resulting screen (text, styles, cursor, modes) is compared with a saved snapshot.
  - **Differential tests,** nightly rather than on every build: the same output replayed through [xterm.js's headless build](https://www.npmjs.com/package/@xterm/headless) or [libghostty-vt](https://github.com/ghostty-org/ghostty), comparing text and cursor first.
  - **Fuzzing** with [SharpFuzz](https://github.com/Metalnem/sharpfuzz) (MIT): the parser alone, then the parser with the screen, checking that the cursor stays in bounds, nothing throws and resizing round-trips.
  - **vttest and esctest,** run against the application from outside. Neither is copied into the repository; esctest is GPL.
  - **Benchmarks** with BenchmarkDotNet on large, colorful and Unicode-heavy output.
- **View models** are tested as plain classes, without Avalonia.
- **Controls and views** are tested with [`Avalonia.Headless.XUnit`](https://docs.avaloniaui.net/docs/testing/headless-xunit) (`[AvaloniaFact]`), including screenshot tests rendered with Skia and compared with saved images. Tests bundle their fonts, so the images are the same on every operating system.
- **Sessions** are tested against real local shells, and SSH against a disposable OpenSSH server in a container.
- **The agent** is tested against a fake `IChatClient` that replays recorded model responses, so tests never call a provider.

## Packaging, signing and updates

- **[Velopack](https://docs.velopack.io/)** builds the installers and delivers updates, including small delta updates, on all three operating systems.
- **Updates are verified before they're applied.** The first prototype checks whether Velopack verifies update packages with a signature of its own; if it doesn't, we sign the update feed ourselves and the application verifies it. HTTPS alone isn't enough for software that runs shells (decision 15).
  - **macOS:** a signed and notarized `.app`, as a `.pkg` and a zip. Signing and notarization run in CI on a macOS runner, from a temporary keychain.
  - **Windows:** a per-user installer that needs no administrator rights, signed with Azure Artifact Signing if Datapoint is eligible, otherwise with an OV certificate kept in a cloud hardware security module.
  - **Linux:** an AppImage, which updates itself, plus `.deb` and `.rpm` packages, which update through the system's package manager.
- **Not used:** MSIX, which wraps the application in a package identity and virtualized file system that get in the way of a terminal starting the user's shells; Flatpak for now, whose sandbox would force every shell to start through `flatpak-spawn --host`; and Avalonia's paid Parcel packager.
- **Distributed by direct download from <https://datapoint.software/terminal/>, for now** (product decision 61), which also serves the update feed. No app stores: the Mac App Store's terms conflict with the AGPL, and its required sandbox rules out starting arbitrary shells.

## Decision log

Decisions in the order they were taken, with the reason. Newest last.

| # | Date | Decision | Why |
| --- | --- | --- | --- |
| 1 | 2026-11-06 | Build the application with .NET and Avalonia. | Datapoint already builds with .NET (Apiary); Avalonia draws its own controls, so it looks the same on every platform and avoids each system's web engine; lighter than Electron. |
| 2 | 2026-11-06 | Write our own terminal emulator, with its core separate from the display. | No maintained .NET emulator exists, and owning it lets us build agent needs (one shared screen model, who caused what, command boundaries, changes since last read) into the model itself. |
| 3 | 2026-11-06 | American English everywhere: names in code, documentation, commit messages and the interface's text. | As in Apiary; it's the language of the frameworks' names and of the license text. |
| 4 | 2026-11-06 | SSH is built into the application with a .NET library, not done by running the system's `ssh`. Refined by 7: our own implementation, not a library. The application reads `~/.ssh/config` itself. | Owning the connection allows extra channels (file transfer, port forwarding, starting the shell with shell integration loaded) the same way on every platform, and host-key confirmations, passwords and passphrases in the application's own windows, away from the terminal the agent reads. |
| 5 | 2026-11-06 | The system's `ssh` is never used, not even as a fallback. `~/.ssh/config` features are covered in order of how common they are; a host that uses one not supported yet fails with a clear error naming the setting (e.g. "`GSSAPIAuthentication` isn't supported yet"). | One code path that behaves the same on every platform, and the errors show which features users actually need. Until a feature is covered, its users connect to those hosts from another terminal. |
| 6 | 2026-11-06 | Adopt the conventions in this file, researched for .NET 10, C# 14 and Avalonia 12.1 and based on Apiary's. | Code that's consistent with Datapoint's other product, and easy for people to read and maintain. |
| 7 | 2026-11-06 | Write our own SSH implementation, with no SSH library; the cryptography itself comes from vetted libraries. Refines 4. | Owning the connection is needed to optimize it fully for the agent, as with the emulator. The protocol is security-critical, hence the rules under [SSH](#ssh): no home-made cryptography, interoperability tests, fuzzing and an independent review. |
| 8 | 2026-11-06 | Cryptography comes from .NET's built-in implementation first and BouncyCastle for the rest, through one `Cryptography` area; we never implement a primitive. A FIPS mode uses only .NET's built-in cryptography from the operating system's validated modules. | .NET 10 lacks algorithms modern SSH needs; one place makes the cryptography auditable and replaceable; FIPS is required by enterprise and government customers. |
| 9 | 2026-11-06 | RoyalTerminal is not used. | Evaluated by the product owner. |
| 10 | 2026-11-06 | An external firm reviews the SSH implementation, starting as soon as transport and login work. | Code that holds users' server access needs independent review, early enough to act on it. |
| 11 | 2026-11-06 | Accessibility in the first release through the automation peer and live regions, with an early test of a hidden mirror text box; in parallel, a text pattern contributed to Avalonia. | Ships something usable now and fixes the gap properly, for Avalonia's other users too. |
| 12 | 2026-11-06 | Windows code signing with Azure Artifact Signing if Datapoint is eligible, otherwise an OV certificate in a cloud hardware security module. | Signed builds avoid SmartScreen warnings; eligibility is for Datapoint to confirm. |
| 13 | 2026-11-06 | SSH features after the transport, login and sessions, in order: `~/.ssh/config`, jump hosts, SSH agent, file transfer, port forwarding, certificates, agent forwarding, then Kerberos and smart cards. | Most common first; the agent also covers hardware security keys. |
| 14 | 2026-11-06 | `bcrypt_pbkdf` is ported from OpenBSD's reference implementation onto BouncyCastle's Blowfish, as the one documented exception to "never implement a primitive". | Neither .NET nor BouncyCastle provides it, and users' encrypted OpenSSH key files need it; test vectors keep it honest, and FIPS mode never uses it. |
| 15 | 2026-11-06 | Update packages are verified with a signature before they're applied: Velopack's, if it has one, or our own on the update feed. Checked in the first prototype. | Software that runs shells on users' machines can't trust HTTPS alone for its updates. |

## Open questions

None at the moment.
