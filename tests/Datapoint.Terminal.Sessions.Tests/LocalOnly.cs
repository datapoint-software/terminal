// These tests start real shells and an SSH server in a container. On the CI runners they hung the Linux job now and then
// and timed out with PowerShell on macOS, for reasons not yet found, so the pipeline skips them (--filter-not-trait
// "Category=LocalOnly" in .github/workflows/ci.yml). Run them locally before changing sessions, the terminal connection or SSH.
[assembly: Trait("Category", "LocalOnly")]
