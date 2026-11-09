# Software the installers bundle

Velopack's `vpk` tool (version pinned in `dotnet-tools.json`) adds its own programs to every package, and NuGet doesn't describe them, so their licenses are kept here, one set per `vpk` version. The notices generator reads them and fails when `vpk` is upgraded until both files exist for the new version.

- `vpk-<version>-rust-crates.json`: the Rust crates compiled into Velopack's `Update` program (every package) and its `Setup` and `stub` programs (Windows).
- `vpk-<version>-appimage-runtime.json`: the [AppImage type-2 runtime](https://github.com/AppImage/type2-runtime) that starts each Linux `.AppImage`, and the libraries it links statically. Velopack's `vendor/THIRD_PARTY_NOTICES.md` (in the `vpk` package) names the runtime build it carries.

Each file lists licenses, each with its text and the components under it. Only licenses on the reviewed list in [docs/engineering.md](../../../docs/engineering.md#build-and-packages) are accepted.

## Regenerating after a vpk upgrade

1. **Rust crates.** In a container with Rust (e.g. `docker run --rm -it rust:1 bash`), with `jq` installed:

   ```bash
   git clone --depth 1 --branch <version> https://github.com/velopack/velopack.git /v
   cargo install --locked cargo-about --features cli
   cd /v/src/bins
   cargo about generate --config about.toml --features windows --format json -o raw.json
   jq '{licenses: [.licenses[] | {id, text, crates: [.used_by[] | {name: .crate.name, version: .crate.version, repository: (.crate.repository // null)}] | unique_by(.name + " " + .version)}]}' raw.json
   ```

   Use `about.toml` from this folder: it accepts the licenses Velopack's own `deny.toml` allows and lists the six targets the application ships. Add a `source` field saying where the data came from, and set the version of Velopack's own crates, which report `0.0.0-local`, to the release.

2. **AppImage runtime.** Find the runtime build named in the new `vpk`'s `vendor/THIRD_PARTY_NOTICES.md`, then the runtime repository's commit from that date. Its `scripts/common/install-dependencies.sh`, `scripts/chroot/build.sh` and `src/runtime/Makefile` show which libraries it links and their versions. Take each license text from that version's source, and update the libfuse source links in `Bundled.cs`.

3. Run `dotnet run --project tools/ThirdPartyNotices` and commit the result with the upgrade.
