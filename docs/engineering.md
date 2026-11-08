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
| Text editor (files on hosts) | [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) 12.0, coloured by our own highlighter | MIT |
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
.githooks/                         Git hooks that stop secrets and real host names or addresses from being committed
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
- **Comments say why, never what.** If code needs a comment to say what it does, rename or restructure it until it doesn't. A short `//` comment is kept where the reason can't be read from the code: a security or safety measure (so nobody "simplifies" it away), a workaround for a bug or platform quirk (with a link where there is one), or a choice that looks wrong but isn't. No commented-out code, and no XML documentation: we don't publish libraries for developers outside Datapoint.
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
- **Third-party notices are generated,** as in Apiary: after adding or upgrading a package, regenerate `THIRD-PARTY-NOTICES.txt` and commit it with the change; CI fails when it's out of date. The generator accepts only reviewed licenses: MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, the Unicode License v3 (for the Unicode data the emulator's width tables are built from) and the SIL Open Font License 1.1 (for the Inter font; approved by Joao Lopes on 2026-11-07). Material a package carries under a license of its own, such as the font inside `Avalonia.Fonts.Inter`, is listed separately with that license. A package under another license fails it until someone has reviewed that license, including whether it still lets Datapoint offer Datapoint Terminal under a commercial license as well as the AGPL.
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
- **Inter is the interface font on every system,** bundled through `Avalonia.Fonts.Inter` (the font is under the SIL Open Font License 1.1). Avalonia sets the macOS interface font about 3 pt above centre in buttons and badges, and the screenshot tests render with Inter, so they show what users see. Terminals and code keep their monospace fonts.
- **Motion goes through `Shell/Motion`, and stops when the user asks.** Popups (menus, drop-downs, tooltips, flyouts) fade in, and menus fade out, through `OverlayMotion`, so a view adds nothing for them; menus are `MenuFlyout`s, not `ContextMenu`s, as only a flyout can put off its closing. A part that appears or goes inside a window uses `motion:Fold.IsOpen` in place of `IsVisible`. Nothing moves while `MotionPreference` says not to: the Reduce motion switch in Appearance, or the system's own setting (macOS, Windows, and GNOME's on Linux). Animations last 100 to 200 ms and are cut short once they should have ended. Avalonia can't fade a native window as a whole, so dialogs open as the system shows them.
- **Sizes are tokens too:** type sizes, corner radii and row, button and field heights in `Styles/Tokens.axaml`, used through `StaticResource`. No size is written in a view, except for a shape of its own (a circle, a bubble).
- **Shared before local.** Text roles, surfaces (cards, grouped rows, bordered lists) and variants of stock controls are app-wide style classes in `Styles/` (`Text.axaml`, `Surfaces.axaml`, `Controls.axaml`), named for their role (`secondary`, `group`, `primary`), not their look. A view's own `Styles` hold only what no other view needs; a second view needing it moves it to `Styles/`.
- **The application's own controls are templated controls with a `ControlTheme`** in `Styles/Components.axaml`, such as `Badge` and `Avatar` in `Components/`. Variants of stock controls stay style classes rather than control themes, because classes combine (`plain danger`) and a control takes only one theme (autopilot decision 107).
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
- **Over SSH** the session runs a one-line bootstrap (`RemoteShellIntegration`) as an exec request in place of the login shell. It turns the terminal's echo off and prints a ready marker; the application then sends the nonce and the scripts as the terminal's input, never on a command line other users of the host can read. They land in a `mktemp -d` folder (mode 700) that the shell deletes while loading them, and the login shell starts with them as it would locally. A host whose login shell isn't bash, zsh or fish, or that lacks `mktemp` or `stty`, or refuses the command, gets its ordinary login shell. A connection can turn it off with `"shellIntegration": false`. Tested against OpenSSH on Alpine (`tests/ssh-server`, which has bash, zsh and fish).

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

### SSH security rules

Researched on 2026-11-06 against OpenSSH's release notes and `PROTOCOL` files, the IETF drafts for strict key exchange and host key updates, Qualys' and PuTTY's advisories, and libssh's. Every rule below is a requirement for our implementation, and each has a test. Sources are listed at the end.

**Transport**

- **Strict key exchange against Terrapin** ([CVE-2023-48795](https://terrapin-attack.com/), [draft-ietf-sshm-strict-kex](https://datatracker.ietf.org/doc/html/draft-ietf-sshm-strict-kex)): we offer `kex-strict-c-v00@openssh.com` and `kex-strict-c`, and turn it on only when the server offers the matching marker, read from its first `KEXINIT` only. The first message received must be `KEXINIT`; during the first key exchange any other message, including `IGNORE`, `DEBUG` and `UNIMPLEMENTED`, disconnects; each key exchange message is accepted only as many times as expected; sequence numbers reset to zero right after `NEWKEYS` is sent and received, on every key exchange.
- **An allow-list of message numbers for each phase** of the connection (key exchange, authentication, connected), not only during key exchange. Anything else disconnects.
- **No CBC and no encrypt-and-MAC with CTR by default.** Ciphers are AES-GCM and `chacha20-poly1305@openssh.com`, then AES-CTR only with an encrypt-then-MAC (`-etm@openssh.com`) HMAC-SHA2. With encrypt-then-MAC, the MAC over the length and ciphertext is checked before anything is decrypted. MACs are compared in constant time, and every integrity failure takes the same path: disconnect, with no further reads.
- **AES-GCM nonces** follow [RFC 5647](https://www.rfc-editor.org/rfc/rfc5647): the invocation counter increases with every packet, and a nonce is never used twice under one key. `chacha20-poly1305@openssh.com` is built from BouncyCastle's ChaCha20 and Poly1305, since .NET's `ChaCha20Poly1305` is the RFC 8439 construction, which OpenSSH's isn't.
- **Rekeying** after 2³¹ packets in either direction, 2³² cipher blocks, 1 GB or one hour, whichever comes first. A sequence number never wraps: we disconnect instead. On every rekey, the server's host key must be byte-for-byte the one verified at first.
- **No compression.** Compression shared across channels can leak one channel's contents through another's packet sizes, and it adds a decompression path to attack; links are fast enough without it.
- **Random numbers come only from `RandomNumberGenerator`.** An SSH library that used a non-cryptographic generator for its X25519 keys let eavesdroppers decrypt sessions ([CVE-2022-29245](https://nvd.nist.gov/vuln/detail/CVE-2022-29245)).

**Key exchange**

- **Points are validated:** NIST curve points only in uncompressed form, of the exact length, with coordinates below the field prime, on the curve and not at infinity, checked explicitly rather than trusted to the import. X25519 keys are exactly 32 bytes, and an all-zero shared secret aborts the exchange ([RFC 8731](https://www.rfc-editor.org/rfc/rfc8731)).
- **`mlkem768x25519-sha256` first,** as in OpenSSH 10: the hybrid protects today's sessions against later decryption by quantum computers. Sizes are checked exactly, and the shared secret is encoded as an SSH string, not an `mpint`, as the specification requires.
- **No Diffie-Hellman group exchange and no SHA-1 key exchange.** Neither is offered, so their weak-modulus and downgrade problems don't arise.
- **No silent fallback:** a host that offers nothing we accept, or a configuration that asks for an algorithm we don't implement, fails with a message naming them.
- **`EXT_INFO`** ([RFC 8308](https://www.rfc-editor.org/rfc/rfc8308)) is accepted only right after the server's first `NEWKEYS`, or just before `USERAUTH_SUCCESS`. RSA keys sign with `rsa-sha2-512` or `rsa-sha2-256`; `ssh-rsa` with SHA-1 only when the host's configuration lists it, never as a fallback when the server doesn't say what it accepts, and never in FIPS mode. A signature whose algorithm differs from the one negotiated is rejected.

**Signatures and certificates**

- **We never derive a signing nonce.** ECDSA nonces come from the platform's random generator or BouncyCastle's RFC 6979 calculator. PuTTY's own derivation biased P-521 nonces, so about 60 signatures revealed the private key ([CVE-2024-31497](https://www.chiark.greenend.org.uk/~sgtatham/putty/wishlist/vuln-p521-bias.html)).
- **Malformed signatures fail cleanly:** zero or out-of-range ECDSA values, oversized integers and Ed25519 signatures with S ≥ L are a failed verification, never an exception.
- **RSA keys shorter than 2048 bits are refused,** unless the host's configuration lowers the limit (`RequiredRSASize`), and never below 1024.
- **Certificates:** a host certificate must be of the host type, within its validity (with a small allowance for clock differences), signed by a CA trusted by an `@cert-authority` line that matches the name the user asked for (or its `HostKeyAlias`), and must name that host among its principals. An empty principals list, which the specification treats as "any host", is refused. Unknown critical options are refused. A CA key can't itself be a certificate, and CA signatures with SHA-1 are refused. `@revoked` entries and revocation lists apply to the certificate's key and to the CA's.

**Host keys**

- **`known_hosts` is read as OpenSSH writes it:** hashed names, `[host]:port`, patterns and negation, `@revoked` and `@cert-authority`. The application writes to it atomically and with a lock, since OpenSSH may write at the same time.
- **The host key is verified before any credential is sent.**
- **A new host key is accepted only by the user,** in the application's own window, showing its SHA-256 fingerprint. **The agent can never accept or change a host key.**
- **A changed host key is a hard failure** (product decision 64). Accepting the new key is a separate, deliberate action showing the old and new fingerprints, never a button on the failed connection. A key listed as `@revoked` can't be accepted at all.
- **Verification fails closed:** results start as "failed", a check passes only on an explicit success, and any exception means failure. [CVE-2025-26465](https://www.qualys.com/2025/02/18/openssh-mitm-dos.txt) let a man-in-the-middle through because an error code other than the expected one was read as success.
- **No SSHFP (`VerifyHostKeyDNS`):** DNSSEC can't be validated the same way on every platform. A configuration that asks for it fails clearly, as for any unsupported setting.
- **Host key updates** (`hostkeys-00@openssh.com`) are accepted only after login, only for a host matched by an exact `known_hosts` entry (no certificate or wildcard), and each new key only after its proof signature checks. The number of keys is capped, and a second announcement in one connection is ignored.

**Login**

- **Credentials are bound to a host, port and user,** and are never reused across the hops of a jump chain. Every prompt names the host it's for.
- **Messages out of order are refused:** before login succeeds, any channel or global request disconnects, and a `USERAUTH_SUCCESS` with no request outstanding is refused.
- **A login that needed no credential is shown as such** ("trivial authentication"), and can be refused per host. A server that lets anyone in and then shows a fake password prompt in the terminal could otherwise collect credentials ([PuTTY's description](https://www.chiark.greenend.org.uk/~sgtatham/putty/wishlist/vuln-auth-prompt-spoofing.html)).
- **Text from the server** (banners, keyboard-interactive names, instructions and prompts, disconnect and debug messages) is shown labelled as coming from that server, with control characters, escape sequences and bidirectional overrides removed and its length capped. It never looks like the application's own text.
- **Passwords, passphrases and keyboard-interactive answers never reach the agent or the provider.**
- **Only the keys configured for a host are offered,** like OpenSSH's `IdentitiesOnly`, when the configuration names any, so servers don't learn every key the user has. No environment variables are sent unless the configuration asks (`SendEnv`).

**Agent forwarding, X11 and port forwarding**

- **Agent forwarding is off unless the host's configuration turns it on,** never goes to jump hosts, and can ask for confirmation on each use. Forwarded requests are filtered: only listing identities, signing and `session-bind@openssh.com` pass. Adding or removing keys, smart card requests and locking are refused ([CVE-2023-38408](https://www.qualys.com/2023/07/19/cve-2023-38408/rce-openssh-forwarded-ssh-agent.txt)).
- **Before signing with an agent key,** the application binds the agent to the session (`session-bind@openssh.com`), so keys restricted to certain hosts stay restricted. The agent's socket or pipe must belong to the user, and every reply from it is validated.
- **No X11 forwarding.** It gives the remote side access to the user's display.
- **Forwarded ports bind to `127.0.0.1` and `::1` explicitly;** binding to all interfaces needs the configuration to ask for it and shows a warning. Incoming `forwarded-tcpip` channels are accepted only for remote forwards we asked for, and connect only to the configured destination. Channel types we didn't ask for (`x11`, `auth-agent@openssh.com`) are refused.

**Files from the server**

- **SFTP writes only what the user or an approved agent action asked for.** Names from the server are refused if they're empty, `.` or `..`, or contain `/`, `\`, NUL or control characters; on Windows also drive letters, `:`, reserved names (`CON`, `NUL`, …) and trailing dots or spaces. The final path is resolved and must stay inside the target folder, without following symbolic links ([CVE-2019-6111](https://nvd.nist.gov/vuln/detail/CVE-2019-6111)).
- **No undocumented or experimental extensions** ([CVE-2016-0777](https://www.qualys.com/2016/01/14/cve-2016-0777-cve-2016-0778/openssh-cve-2016-0777-cve-2016-0778.txt) leaked private keys through one).

**Configuration and host names**

- **Host and user names from anywhere but the configuration** (the user's input, `ssh://` links, the agent, terminal output) must pass a strict allow-list: host names as letters, digits, hyphens and dots, IP addresses and `[IPv6]`; user names as letters, digits, `.`, `_` and `-`. Nothing that starts with `-`, and no whitespace, control characters, NUL or shell metacharacters. Several OpenSSH and libssh vulnerabilities came from such names reaching a shell through `%h`, `%r` or `%u` ([CVE-2023-51385](https://nvd.nist.gov/vuln/detail/CVE-2023-51385) and its successors in 2025 and 2026).
- **A host name the agent found in terminal output** (product decision 72) is shown to the user to confirm, exactly as it will be used, before a tab is opened.
- **`ProxyCommand`, `Match exec`, `LocalCommand` and `KnownHostsCommand` run programs.** They're honored only from the user's configuration files, the expanded command is shown the first time it runs for a host, and expansions are quoted for the shell that runs them (`/bin/sh` or `cmd.exe`, which quote differently). `LocalCommand` stays off unless `PermitLocalCommand` turns it on. Host keys are still checked end to end through a `ProxyCommand`.
- **Configuration files others can write are refused,** including through `Include` (permissions and owner on macOS and Linux, access lists on Windows). On Windows, system configuration is read from `%ProgramData%\ssh`, never from Unix paths, where any user could create `C:\etc`.

**Parsing and limits**

- **Everything from the network is bounded before anything is allocated:** the version line and any lines before it (255 bytes each, and a limit on how many), packets (256 KB), padding (at least 4 bytes, smaller than the packet, block-aligned), and each string, name-list and `mpint`. Lengths are checked with unsigned, overflow-checked arithmetic.
- **Pre-login queues are bounded,** and `ping@openssh.com` is ignored before login and during key exchange ([CVE-2025-26466](https://www.qualys.com/2025/02/18/openssh-mitm-dos.txt)).
- **Channels:** a window never grows past 2³²−1, data beyond the window disconnects, a maximum packet size of zero or too small to make progress is refused, and the number of channels and pending global requests is capped.
- **Secrets in memory** (private keys, passwords, session keys) live in byte arrays that are wiped after use, never in strings, and never reach logs or crash reports. A private key file's key derivation parameters (e.g. `bcrypt_pbkdf` rounds) are checked against limits before use.

**Timing**

- **The user's typing is sent at a fixed interval with decoy packets** when the server supports `ping@openssh.com`, as OpenSSH 9.5 and later do (`ObscureKeystrokeTiming`), so an observer can't learn passwords from keystroke timing. What the agent sends goes in one packet per command, which reveals only its length.

**Sources:** [OpenSSH release notes](https://www.openssh.org/releasenotes.html); [OpenSSH `PROTOCOL`](https://github.com/openssh/openssh-portable/blob/master/PROTOCOL) and [certificate draft](https://datatracker.ietf.org/doc/draft-miller-ssh-cert/); [draft-ietf-sshm-strict-kex](https://datatracker.ietf.org/doc/html/draft-ietf-sshm-strict-kex); [draft-ietf-sshm-hostkey-update](https://datatracker.ietf.org/doc/html/draft-ietf-sshm-hostkey-update); [Terrapin](https://terrapin-attack.com/); [Qualys on CVE-2025-26465 and 26466](https://www.qualys.com/2025/02/18/openssh-mitm-dos.txt); [PuTTY's changes](https://www.chiark.greenend.org.uk/~sgtatham/putty/changes.html) and [vulnerability list](https://www.chiark.greenend.org.uk/~sgtatham/putty/wishlist/); [libssh's advisories](https://www.libssh.org/security/advisories/); RFCs 4253, 4344, 5647, 8270, 8308 and 8731. Some 2025–2026 CVE details were confirmed only through distributions' trackers; the external review (decision 10) checks this list again.

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
- **Anthropic's prompt caching** is used through the SDK's own `IChatClient`, which is the only one that can mark it: `AnthropicPromptCache` marks the end of the system prompt and of the newest message on each request, so each step of the agent's loop reads the tools, the system prompt and the conversation so far from the cache. OpenAI caches a repeated prefix by itself; OpenAI-compatible servers do as they do. The usage meter prices cache reads and writes separately: the user's cached input price if given, else Anthropic's published rates for Anthropic (a tenth of input to read, 1.25 times to write) and the full input price for others, which may overstate the cost but never understates it against a spending limit.
- **The Anthropic SDK is pinned and kept behind our adapter.** It publishes breaking major versions often.
- **Web research** (product decision 105) is two of our own tools, `search_web` and `read_page`, not provider-side tools, so the application can ask the user before each one:
  - **Search** goes to the service the user chose: the provider's own search, called with `HostedWebSearchTool` in a separate request that holds only the query (Anthropic and OpenAI; OpenAI-compatible servers have none), or Brave Search, Tavily or a SearXNG server over their JSON APIs. The provider's search is recorded by the usage meter like any request.
  - **Pages are read by the application,** with `WebAddressGuard` and `WebPageReader`: http and https only; no loopback, private, link-local, shared, documentation or multicast addresses, IPv4 embedded in IPv6 judged as IPv4; checked again on every redirect; and connected through a `ConnectCallback` to the very addresses that were checked, so a name can't resolve to a private address the second time. No cookies, credentials or proxy, a 20-second timeout and a 3 MB limit.
  - **HTML becomes Markdown with our own scanner** (`HtmlText`), not a parser library: reading only needs it to be good enough, it stays linear on hostile pages, and it adds no dependency to review. It keeps headings, paragraphs, lists, code, tables and links, prefers `main` or `article`, and drops scripts, styles, navigation and forms.
  - **Prompt injection guards** (product decision 106) live in `TerminalAgent.WebGuard`: the agent keeps the addresses it didn't write (search results, page links, the user's messages and their hosts) and, in `SeenText`, what the terminals showed this conversation (normalized, capped at 256 KB). An address or a network command (`CommandClassifier.IsNetwork`) sharing 24 or more characters with that text, after URL decoding, asks and quotes it; an exact repeat of something a terminal printed doesn't count. After any web text, network commands ask even in Auto, and their approval can't be carried. `InjectionCheck` is a regular expression for the usual openings of an injection: it adds a warning to the tool result and the chat, and never removes text. All of it is reset with the conversation.

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
- **Workspaces** (product decisions 100 to 103) are folders the user chooses, with a fixed layout, and hold only what's meant to be shared:
  - `workspace.json`: hosts, their environments, labels and models; System.Text.Json with source generation. It has no field for a secret, and its content goes through the secret detector before it's saved.
  - `TERMINAL.md`, `hosts/<name>/README.md`, `hosts/<name>/log/*.md` and `skills/<name>/SKILL.md`: Markdown, so users can read, edit, search and version them (product decision 38). The application watches the folder and reloads files changed outside it.
  - **Writes** go to a temporary file outside the workspace, in the application's data, then are moved into place, so a sync service never sees half a file. A move is only atomic within one disk: when the workspace is on another disk, the temporary file goes in the workspace's own folder under a name sync services skip (which names, for OneDrive, Dropbox and Google Drive, to be checked before this is built).
  - **Conflicting copies** that sync services leave beside a file (e.g. `README-LAPTOP.md`, `README (conflicted copy).md`) are recognized by name and shown as one item to settle.
- **Ignore files:** `.terminalignore`, in `.gitignore` syntax, matched by our own implementation of Git's rules, never by running `git`, read from the home folder down to each file's folder (from the workspace's folder when it's outside the home folder), so a file in a shared folder above can't decide for the user.
- **What stays in the application's data,** by workspace, never in a workspace folder: providers, the list of workspaces, memory, conversation history and scrollback, session recordings, the host keys last seen per host, the copies of instructions and skills last accepted, "not a secret" choices (as salted hashes), and the record of each host's last confirmed security settings (product decisions 92 and 101), so nothing in a workspace folder can vouch for itself.
- **Conversations and history:** SQLite through Microsoft.Data.Sqlite, in the application's data, one database per workspace, with full-text search. A lock in the same place keeps a second copy of the application from opening the same workspace for writing; a locked workspace opens read-only.
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
- **To be distributed by direct download from <https://datapoint.software/terminal/>, for now** (product decision 61), which will also serve the update feed. Nothing is published there yet. No app stores: the Mac App Store's terms conflict with the AGPL, and its required sandbox rules out starting arbitrary shells.

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
| 16 | 2026-11-06 | Adopt the [SSH security rules](#ssh-security-rules): strict key exchange, no CBC, compression, group exchange, SHA-1 key exchange, SSHFP or X11 forwarding; host keys accepted only by the user; strict names from untrusted sources; bounded parsing. | Each rule answers a published attack on SSH clients; leaving features out removes whole classes of them. |
| 17 | 2026-11-08 | Git hooks in `.githooks/` check what's committed, and each commit message, for secrets and real infrastructure details: API keys and tokens, long base64 such as a private key, IP addresses outside the documentation ranges, and the words a clone lists in `.git/info/forbidden`. Public values that look like these are listed in `.githooks/allowed`. They're written in POSIX shell and awk, and each clone turns them on with `git config core.hooksPath .githooks`. | The repository is public, and a real host name and address once came in through realistic test examples and had to be removed by rewriting the history. Shell and awk come with Git on every platform, so there's nothing to install; a scanner such as gitleaks would need installing on every machine, and its rules for internal names would have to be committed, publishing those names. The forbidden words stay in each clone for the same reason. |

## Open questions

None at the moment.
